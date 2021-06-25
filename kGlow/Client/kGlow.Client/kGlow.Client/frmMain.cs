﻿using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Windows.Forms;

namespace kGlow.Client
{
    public partial class frmMain : Form
    {
        public frmMain()
        {
            InitializeComponent();

            CheckDriver();
        }

        Thread glowThread;

        private bool IsDriverRunning()
        {
            try
            {
                ServiceController sc = new ServiceController("kGlow_Driver");

                switch (sc.Status)
                {
                    case ServiceControllerStatus.Running:
                        return true;
                    case ServiceControllerStatus.Stopped:
                        return false;
                    case ServiceControllerStatus.Paused:
                        return false;
                }

                return false;
            }
            catch { return false; }
        }

        private void CheckDriver()
        {
            if (IsDriverRunning())
            {
                DriverStatus(true);
            }
            else
            {
                DriverStatus(false);
            }
        }


        private void btnStartDriver_Click(object sender, EventArgs e)
        {
            var result = new frmProcess(Program.JobType.StartDriver).ShowDialog();
            if (result == DialogResult.Cancel)
            {
                errorFader.Start();
                pnlError.Show();
            }
            else
            {
                DriverStatus(true);
            }
        }

        private void btnRunWithDSEFix_Click(object sender, EventArgs e)
        {
            var confirm = new frmDSEWarn().ShowDialog();

            if (confirm == DialogResult.Yes)
            {
                var result = new frmProcess(Program.JobType.StartDSEFix).ShowDialog();
                if (result == DialogResult.Cancel)
                {
                    errorFader.Start();
                    pnlError.Show();
                }
                else
                {
                    DriverStatus(true);
                    lblDriverStatus.Text = "Running (DSEFix)";
                }
            }
        }

        private void btnStopDriver_Click(object sender, EventArgs e)
        {
            var result = new frmProcess(Program.JobType.StopDriver).ShowDialog();
            if (result == DialogResult.Cancel)
            {
                errorFader.Start();
                pnlError.Show();
            }
            else
            {
                GlowStatus(false);
                DriverStatus(false);
            }
        }
        
        private void GlowStatus(bool on)
        {
            if(on)
            {
                lblGlowStatus.Text = "Active";
                btnGlowOn.Enabled = false;
                btnGlowOff.Enabled = true;
            }
            else
            {
                lblGlowStatus.Text = "Inactive";
                btnGlowOn.Enabled = true;
                btnGlowOff.Enabled = false;
            }
        }

        private void DriverStatus(bool on)
        {
            if (on)
            {
                lblDriverStatus.Text = "Running";
                btnGlowOn.Enabled = true;
                btnGlowOff.Enabled = false;

                btnStopDriver.Enabled = true;
                btnStartDriver.Enabled = false;
                btnRunWithDSEFix.Enabled = false;
                btnDisableDSE.Enabled = false;
            }
            else
            {
                lblDriverStatus.Text = "Not Running";
                btnGlowOn.Enabled = false;
                btnGlowOff.Enabled = false;

                btnStopDriver.Enabled = false;
                btnStartDriver.Enabled = true;
                btnRunWithDSEFix.Enabled = true;
                btnDisableDSE.Enabled = true;
            }
        }
        private void errorFader_Tick(object sender, EventArgs e)
        {
            errorFader.Stop();
            pnlError.Hide();
        }

        private void btnDisableDSE_Click(object sender, EventArgs e)
        {
            var confirm = MessageBox.Show("This setting will disable DSE indefinitely until you manually re-enable it. This feature is only recommended to be used with an alt account or with VAC disabled because VAC will detect this modification.\n\nContinue?", "Disabling Signature Enforcement", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if(confirm == DialogResult.Yes)
            {
                var result = new frmProcess(Program.JobType.DisableDSE).ShowDialog();
            }
        }

        private void btnGlowOff_Click(object sender, EventArgs e)
        {
            try 
            { 
                glowThread.Abort();
                glowThread = null;
                GlowStatus(false);
            }
            catch { }
        }

        private void btnGlowOn_Click(object sender, EventArgs e)
        {
            glowThread = new Thread(DoGlow);
            glowThread.Start();

            GlowStatus(true);
        }


        [DllImport(@"kGlow.Interface.dll")]
        static extern int ReadInteger(int ProcessID, int Address);

        [DllImport(@"kGlow.Interface.dll")]
        static extern bool ReadBoolean(int ProcessID, int Address);

        [DllImport(@"kGlow.Interface.dll")]
        static extern bool WriteInteger(int ProcessID, int Address, int Value);

        [DllImport(@"kGlow.Interface.dll")]
        static extern bool WriteFloat(int ProcessID, int Address, float Value);

        [DllImport(@"kGlow.Interface.dll")]
        static extern bool WriteBoolean(int ProcessID, int Address, bool Value);

        [DllImport(@"kGlow.Interface.dll")]
        static extern int GetPID();

        [DllImport(@"kGlow.Interface.dll")]
        static extern int GetModule();

        private void DoGlow()
        {

            lblGlowStatus.Text = "Waiting";

            Process[] p = Process.GetProcessesByName("csgo");

            while (p.Length <= 0)
                p = Process.GetProcessesByName("csgo");


            lblGlowStatus.Text = "Active";

            int PID = GetPID();
            int MOD = GetModule();

            while (true)
            {
                int LocalPlayer = ReadInteger(PID, MOD + signatures.dwLocalPlayer);
                int lpTeam = ReadInteger(PID, LocalPlayer + netvars.m_iTeamNum);
                int GlowObject = ReadInteger(PID, MOD + signatures.dwGlowObjectManager);

                if (lpTeam == 0x1 || lpTeam == 0x0)
                    continue;

                for (int i = 1; i <= 32; i++)
                {
                    int Entity = ReadInteger(PID, MOD + signatures.dwEntityList + i * 0x10);

                    if (Entity != 0)
                    {
                        int eTeam = ReadInteger(PID, Entity + netvars.m_iTeamNum);
                        int eGlow = ReadInteger(PID, Entity + netvars.m_iGlowIndex);
                        bool eDormant = ReadBoolean(PID, Entity + signatures.m_bDormant);

                        if (eDormant)
                            continue;

                        if (lpTeam == eTeam)
                        {
                            WriteFloat(PID, GlowObject + eGlow * 0x38 + 0x4, 0);
                            WriteFloat(PID, GlowObject + eGlow * 0x38 + 0x8, 1);
                            WriteFloat(PID, GlowObject + eGlow * 0x38 + 0xC, 0);
                            WriteFloat(PID, GlowObject + eGlow * 0x38 + 0x10, 0.7f);

                            WriteBoolean(PID, GlowObject + eGlow * 0x38 + 0x24, true);
                            WriteBoolean(PID, GlowObject + eGlow * 0x38 + 0x25, false);
                        }
                        else
                        {
                            WriteFloat(PID, GlowObject + eGlow * 0x38 + 0x4, 1);
                            WriteFloat(PID, GlowObject + eGlow * 0x38 + 0x8, 0);
                            WriteFloat(PID, GlowObject + eGlow * 0x38 + 0xC, 0);
                            WriteFloat(PID, GlowObject + eGlow * 0x38 + 0x10, 0.7f);

                            WriteBoolean(PID, GlowObject + eGlow * 0x38 + 0x24, true);
                            WriteBoolean(PID, GlowObject + eGlow * 0x38 + 0x25, false);
                        }
                    }
                }
            }
        }

        public static class netvars
        {
            public const Int32 cs_gamerules_data = 0x0;
            public const Int32 m_ArmorValue = 0xB378;
            public const Int32 m_Collision = 0x320;
            public const Int32 m_CollisionGroup = 0x474;
            public const Int32 m_Local = 0x2FBC;
            public const Int32 m_MoveType = 0x25C;
            public const Int32 m_OriginalOwnerXuidHigh = 0x31C4;
            public const Int32 m_OriginalOwnerXuidLow = 0x31C0;
            public const Int32 m_SurvivalGameRuleDecisionTypes = 0x1320;
            public const Int32 m_SurvivalRules = 0xCF8;
            public const Int32 m_aimPunchAngle = 0x302C;
            public const Int32 m_aimPunchAngleVel = 0x3038;
            public const Int32 m_angEyeAnglesX = 0xB37C;
            public const Int32 m_angEyeAnglesY = 0xB380;
            public const Int32 m_bBombPlanted = 0x99D;
            public const Int32 m_bFreezePeriod = 0x20;
            public const Int32 m_bGunGameImmunity = 0x3944;
            public const Int32 m_bHasDefuser = 0xB388;
            public const Int32 m_bHasHelmet = 0xB36C;
            public const Int32 m_bInReload = 0x32A5;
            public const Int32 m_bIsDefusing = 0x3930;
            public const Int32 m_bIsQueuedMatchmaking = 0x74;
            public const Int32 m_bIsScoped = 0x3928;
            public const Int32 m_bIsValveDS = 0x75;
            public const Int32 m_bSpotted = 0x93D;
            public const Int32 m_bSpottedByMask = 0x980;
            public const Int32 m_bStartedArming = 0x33F0;
            public const Int32 m_bUseCustomAutoExposureMax = 0x9D9;
            public const Int32 m_bUseCustomAutoExposureMin = 0x9D8;
            public const Int32 m_bUseCustomBloomScale = 0x9DA;
            public const Int32 m_clrRender = 0x70;
            public const Int32 m_dwBoneMatrix = 0x26A8;
            public const Int32 m_fAccuracyPenalty = 0x3330;
            public const Int32 m_fFlags = 0x104;
            public const Int32 m_flC4Blow = 0x2990;
            public const Int32 m_flCustomAutoExposureMax = 0x9E0;
            public const Int32 m_flCustomAutoExposureMin = 0x9DC;
            public const Int32 m_flCustomBloomScale = 0x9E4;
            public const Int32 m_flDefuseCountDown = 0x29AC;
            public const Int32 m_flDefuseLength = 0x29A8;
            public const Int32 m_flFallbackWear = 0x31D0;
            public const Int32 m_flFlashDuration = 0xA420;
            public const Int32 m_flFlashMaxAlpha = 0xA41C;
            public const Int32 m_flLastBoneSetupTime = 0x2924;
            public const Int32 m_flLowerBodyYawTarget = 0x3A90;
            public const Int32 m_flNextAttack = 0x2D70;
            public const Int32 m_flNextPrimaryAttack = 0x3238;
            public const Int32 m_flSimulationTime = 0x268;
            public const Int32 m_flTimerLength = 0x2994;
            public const Int32 m_hActiveWeapon = 0x2EF8;
            public const Int32 m_hMyWeapons = 0x2DF8;
            public const Int32 m_hObserverTarget = 0x338C;
            public const Int32 m_hOwner = 0x29CC;
            public const Int32 m_hOwnerEntity = 0x14C;
            public const Int32 m_iAccountID = 0x2FC8;
            public const Int32 m_iClip1 = 0x3264;
            public const Int32 m_iCompetitiveRanking = 0x1A84;
            public const Int32 m_iCompetitiveWins = 0x1B88;
            public const Int32 m_iCrosshairId = 0xB3E4;
            public const Int32 m_iEntityQuality = 0x2FAC;
            public const Int32 m_iFOV = 0x31E4;
            public const Int32 m_iFOVStart = 0x31E8;
            public const Int32 m_iGlowIndex = 0xA438;
            public const Int32 m_iHealth = 0x100;
            public const Int32 m_iItemDefinitionIndex = 0x2FAA;
            public const Int32 m_iItemIDHigh = 0x2FC0;
            public const Int32 m_iMostRecentModelBoneCounter = 0x2690;
            public const Int32 m_iObserverMode = 0x3378;
            public const Int32 m_iShotsFired = 0xA390;
            public const Int32 m_iState = 0x3258;
            public const Int32 m_iTeamNum = 0xF4;
            public const Int32 m_lifeState = 0x25F;
            public const Int32 m_nFallbackPaintKit = 0x31C8;
            public const Int32 m_nFallbackSeed = 0x31CC;
            public const Int32 m_nFallbackStatTrak = 0x31D4;
            public const Int32 m_nForceBone = 0x268C;
            public const Int32 m_nTickBase = 0x3430;
            public const Int32 m_rgflCoordinateFrame = 0x444;
            public const Int32 m_szCustomName = 0x303C;
            public const Int32 m_szLastPlaceName = 0x35B4;
            public const Int32 m_thirdPersonViewAngles = 0x31D8;
            public const Int32 m_vecOrigin = 0x138;
            public const Int32 m_vecVelocity = 0x114;
            public const Int32 m_vecViewOffset = 0x108;
            public const Int32 m_viewPunchAngle = 0x3020;
        }
        public static class signatures
        {
            public const Int32 anim_overlays = 0x2980;
            public const Int32 clientstate_choked_commands = 0x4D28;
            public const Int32 clientstate_delta_ticks = 0x174;
            public const Int32 clientstate_last_outgoing_command = 0x4D24;
            public const Int32 clientstate_net_channel = 0x9C;
            public const Int32 convar_name_hash_table = 0x2F0F8;
            public const Int32 dwClientState = 0x58ADD4;
            public const Int32 dwClientState_GetLocalPlayer = 0x180;
            public const Int32 dwClientState_IsHLTV = 0x4D40;
            public const Int32 dwClientState_Map = 0x28C;
            public const Int32 dwClientState_MapDirectory = 0x188;
            public const Int32 dwClientState_MaxPlayer = 0x388;
            public const Int32 dwClientState_PlayerInfo = 0x52B8;
            public const Int32 dwClientState_State = 0x108;
            public const Int32 dwClientState_ViewAngles = 0x4D88;
            public const Int32 dwEntityList = 0x4D523FC;
            public const Int32 dwForceAttack = 0x318393C;
            public const Int32 dwForceAttack2 = 0x3183948;
            public const Int32 dwForceBackward = 0x3183978;
            public const Int32 dwForceForward = 0x3183954;
            public const Int32 dwForceJump = 0x51FC0A4;
            public const Int32 dwForceLeft = 0x318396C;
            public const Int32 dwForceRight = 0x3183990;
            public const Int32 dwGameDir = 0x6296F8;
            public const Int32 dwGameRulesProxy = 0x526F39C;
            public const Int32 dwGetAllClasses = 0xD641F4;
            public const Int32 dwGlobalVars = 0x58AAD8;
            public const Int32 dwGlowObjectManager = 0x529A258;
            public const Int32 dwInput = 0x51A39B8;
            public const Int32 dwInterfaceLinkList = 0x906E94;
            public const Int32 dwLocalPlayer = 0xD3DBEC;
            public const Int32 dwMouseEnable = 0xD43790;
            public const Int32 dwMouseEnablePtr = 0xD43760;
            public const Int32 dwPlayerResource = 0x3181CE0;
            public const Int32 dwRadarBase = 0x518716C;
            public const Int32 dwSensitivity = 0xD4362C;
            public const Int32 dwSensitivityPtr = 0xD43600;
            public const Int32 dwSetClanTag = 0x89FB0;
            public const Int32 dwViewMatrix = 0x4D43D44;
            public const Int32 dwWeaponTable = 0x51A4478;
            public const Int32 dwWeaponTableIndex = 0x325C;
            public const Int32 dwYawPtr = 0xD433F0;
            public const Int32 dwZoomSensitivityRatioPtr = 0xD48638;
            public const Int32 dwbSendPackets = 0xD415A;
            public const Int32 dwppDirect3DDevice9 = 0xA7030;
            public const Int32 find_hud_element = 0x2AD83520;
            public const Int32 force_update_spectator_glow = 0x3A20E2;
            public const Int32 interface_engine_cvar = 0x3E9EC;
            public const Int32 is_c4_owner = 0x3AEB80;
            public const Int32 m_bDormant = 0xED;
            public const Int32 m_flSpawnTime = 0xA370;
            public const Int32 m_pStudioHdr = 0x294C;
            public const Int32 m_pitchClassPtr = 0x5187408;
            public const Int32 m_yawClassPtr = 0xD433F0;
            public const Int32 model_ambient_min = 0x58DE4C;
            public const Int32 set_abs_angles = 0x1D62D0;
            public const Int32 set_abs_origin = 0x1D6110;
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            Environment.Exit(0);
        }
    }
}

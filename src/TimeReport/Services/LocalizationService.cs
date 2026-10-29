using System.Globalization;
using Avalonia;

namespace TimeReport.Services;

public sealed record LanguageOption(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public static class LocalizationService
{
    private static readonly Dictionary<string, string[]> Weekdays = new()
    {
        ["en"] = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"],
        ["sv"] = ["söndag", "måndag", "tisdag", "onsdag", "torsdag", "fredag", "lördag"],
    };

    private static readonly Dictionary<string, string[]> WeekdaysShort = new()
    {
        ["en"] = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"],
        ["sv"] = ["sön", "mån", "tis", "ons", "tor", "fre", "lör"],
    };

    private static readonly Dictionary<string, string[]> Months = new()
    {
        ["en"] =
        [
            "January", "February", "March", "April", "May", "June",
            "July", "August", "September", "October", "November", "December",
        ],
        ["sv"] =
        [
            "januari", "februari", "mars", "april", "maj", "juni",
            "juli", "augusti", "september", "oktober", "november", "december",
        ],
    };

    private static readonly Dictionary<string, Dictionary<string, string>> Strings = new()
    {
        ["en"] = new Dictionary<string, string>
        {
            ["MenuFile"] = "File",
            ["MenuData"] = "Data",
            ["MenuHelp"] = "Help",
            ["MenuDatabaseLocation"] = "Database location…",
            ["MenuBackup"] = "Backup database…",
            ["MenuRestore"] = "Restore database…",
            ["MenuExit"] = "Exit",
            ["MenuProjects"] = "Projects…",
            ["MenuRefresh"] = "Refresh",
            ["MenuAbout"] = "About TimeReport",
            ["HeaderSubtitle"] = "Local time reporting",
            ["ButtonProjects"] = "Projects",
            ["ButtonDatabase"] = "Database",
            ["ButtonBackup"] = "Backup",
            ["ButtonRestore"] = "Restore",
            ["ButtonAbout"] = "About",
            ["TabTimesheet"] = "Timesheet",
            ["TabTotals"] = "Totals",
            ["ButtonThisWeek"] = "This week",
            ["ButtonAddEntry"] = "+ Add entry",
            ["ButtonAdd"] = "+ Add",
            ["ButtonEdit"] = "Edit",
            ["ButtonDelete"] = "Delete",
            ["ButtonSave"] = "Save",
            ["ButtonCancel"] = "Cancel",
            ["ButtonClose"] = "Close",
            ["ButtonCurrent"] = "Current",
            ["ButtonUseDefault"] = "Use default",
            ["ButtonBrowseFolder"] = "Browse folder…",
            ["LabelWorked"] = "Worked",
            ["LabelLunch"] = "Lunch",
            ["LabelAway"] = "Away",
            ["LabelXPause"] = "X-Pause",
            ["LabelDaysWorked"] = "Days worked",
            ["LabelDaysAway"] = "Days away",
            ["LabelType"] = "Type",
            ["LabelProjectReason"] = "Project / reason",
            ["LabelStart"] = "Start",
            ["LabelEnd"] = "End",
            ["LabelDuration"] = "Duration",
            ["LabelPeriod"] = "Period",
            ["PeriodWeek"] = "Week",
            ["PeriodMonth"] = "Month",
            ["PeriodYear"] = "Year",
            ["PeriodAllTime"] = "All time",
            ["LabelToday"] = "Today",
            ["EmptyEntries"] = "No entries yet",
            ["StatusDatabase"] = "Database:",
            ["StatusLocalOnly"] = "All data stays on this computer",
            ["LabelWeekPrefix"] = "Week",
            ["LabelDate"] = "Date",
            ["LabelTime"] = "Time",
            ["Label24Hour"] = "24-hour clock",
            ["LabelProject"] = "Project",
            ["LabelAwayReason"] = "Away reason",
            ["LabelNewProject"] = "New…",
            ["EntryKindWork"] = "Work",
            ["EntryKindLunch"] = "Lunch",
            ["EntryKindAway"] = "Away",
            ["EntryKindXPause"] = "X-Pause",
            ["EntryDialogAddTitle"] = "Add entry",
            ["EntryDialogEditTitle"] = "Edit entry",
            ["EntryDialogInfoWork"] =
                "Work time is counted. Lunch that overlaps work is deducted; X-Pause is tracked separately.",
            ["EntryDialogInfoLunch"] =
                "Lunch is deducted from work time when it overlaps a work entry.",
            ["EntryDialogInfoAway"] =
                "Away time is tracked separately and is not counted as work.",
            ["EntryDialogInfoXPause"] =
                "X-Pause is a separate weekly benefit. It must be inside work hours, " +
                "in 15-minute steps, max 1:00 per week, and is not deducted from worked time.",
            ["EntryDialogXPauseHint"] =
                "X-Pause this week: {0} used of 1:00, {1} remaining.{2}",
            ["EntryDialogXPauseWeekdayNote"] = " X-Pause is only available Monday–Friday.",
            ["EntryDialogInvalidDate"] =
                "Enter a valid date to see the weekly X-Pause balance.",
            ["EntryDialogChooseProject"] =
                "Choose a project. Use “New…” to create one if needed.",
            ["ProjectsTitle"] = "Projects",
            ["ProjectsHeaderProject"] = "Project",
            ["ProjectsHeaderStatus"] = "Status",
            ["ProjectsHeaderEntries"] = "Entries",
            ["ProjectStatusActive"] = "Active",
            ["ProjectStatusArchived"] = "Archived",
            ["ButtonRename"] = "Rename",
            ["ButtonArchive"] = "Archive",
            ["ButtonUnarchive"] = "Unarchive",
            ["DatabaseLocationTitle"] = "Database location",
            ["CurrentDatabase"] = "Current database",
            ["NewDatabaseFile"] = "New database file",
            ["CopyCurrentDatabase"] = "Copy the current database to the new location",
            ["DatabaseLocationInfo"] =
                "If the selected file already exists, it will be opened instead of overwritten. " +
                "The current database file is left untouched.",
            ["StatusChooseDatabase"] = "Enter a valid database path.",
            ["StatusCurrentDatabase"] = "This is the current database.",
            ["StatusExistingDatabase"] =
                "An existing TimeReport database will be opened. The current database file is left untouched.",
            ["StatusNewDatabase"] = "A new database will be created at this location.",
            ["StatusInvalidDatabase"] = "This file cannot be used: {0}",
            ["AboutDescription"] =
                "A local-first time report. No account, cloud or telemetry.",
            ["AboutDatabase"] = "Database",
            ["AboutDataFolder"] = "Data folder",
            ["AboutSettingsFile"] = "Settings file",
            ["AboutSchemaVersion"] = "Schema version: {0}",
            ["AboutOpenDataFolder"] = "Open data folder",
            ["MessageOK"] = "OK",
            ["MessageCancel"] = "Cancel",
            ["MessageDeleteEntryTitle"] = "Delete entry",
            ["MessageDeleteEntry"] = "Delete this {0} entry?",
            ["MessageCannotDeleteEntry"] = "Cannot delete entry",
            ["MessageCannotLoadEntries"] = "Cannot load entries",
            ["MessageBackupComplete"] = "Backup complete",
            ["MessageBackupSaved"] = "Backup saved to:\n{0}",
            ["MessageBackupFailed"] = "Backup failed",
            ["MessageRestoreTitle"] = "Restore database",
            ["MessageRestoreConfirm"] =
                "Restoring replaces all current data with the selected backup.\n\n" +
                "A backup of the current database will be created automatically first.\n\n" +
                "Continue?",
            ["MessageRestoreComplete"] = "Restore complete",
            ["MessageRestoreDone"] =
                "Data restored.\n\nThe previous database was backed up to:\n{0}",
            ["MessageRestoreFailed"] = "Restore failed",
            ["MessageDatabaseLocationChanged"] = "Database location changed",
            ["MessageNowUsing"] = "TimeReport is now using:\n{0}",
            ["MessageCannotChangeDatabase"] = "Cannot change database location",
            ["MessageCannotCreateProject"] = "Cannot create project",
            ["MessageCannotRenameProject"] = "Cannot rename project",
            ["MessageCannotChangeProject"] = "Cannot change project",
            ["MessageCannotDeleteProject"] = "Cannot delete project",
            ["MessageDeleteProjectTitle"] = "Delete project",
            ["MessageDeleteProject"] =
                "Delete \"{0}\"?\n\nProjects with time entries can only be archived.",
            ["MessageCannotSaveEntry"] = "Cannot save entry",
            ["MessageCannotUseDatabase"] = "Cannot use this database",
            ["MessageCannotOpenDataFolder"] = "Cannot open data folder",
            ["MessageCannotOpenDropbox"] = "Cannot open Dropbox App Console",
            ["NewProjectTitle"] = "New project",
            ["ProjectNamePrompt"] = "Project name:",
            ["RenameProjectTitle"] = "Rename project",
            ["AboutTitle"] = "About TimeReport",
            ["VersionLabel"] = "Version",
            ["MenuDropbox"] = "Dropbox sync…",
            ["MenuSyncNow"] = "Sync now",
            ["ButtonDropbox"] = "Dropbox",
            ["ButtonSync"] = "Sync",
            ["DropboxSettingsTitle"] = "Dropbox sync",
            ["DropboxAccessToken"] = "Access token",
            ["DropboxRemotePath"] = "Remote path",
            ["DropboxRemotePathHint"] = "Example: /TimeReport/timereport.db",
            ["DropboxAutoSync"] = "Sync automatically after changes",
            ["DropboxInstructions"] =
                "Create a Dropbox app with files.content.read, files.content.write and files.metadata.read. " +
                "Add the redirect URI below to the app, then enter the app key and connect.",
            ["DropboxLocalCopyInfo"] = "Your local database remains the working copy. Dropbox is used for synchronization.",
            ["DropboxOpenAppConsole"] = "Open Dropbox App Console",
            ["DropboxTestConnection"] = "Test connection",
            ["DropboxTesting"] = "Testing connection…",
            ["DropboxConnected"] = "Connected to Dropbox.",
            ["DropboxTokenRequired"] = "Enter a Dropbox access token.",
            ["DropboxSaved"] = "Dropbox settings saved.",
            ["DropboxStatusOff"] = "Dropbox: off",
            ["DropboxStatusReady"] = "Dropbox: ready",
            ["DropboxStatusSyncing"] = "Dropbox: syncing…",
            ["DropboxStatusSynced"] = "Dropbox: synced",
            ["DropboxStatusUpToDate"] = "Dropbox: up to date",
            ["DropboxStatusConflict"] = "Dropbox: conflict",
            ["DropboxStatusError"] = "Dropbox: error",
            ["DropboxStatusDisabled"] = "Dropbox: auto-sync off",
            ["DropboxStatusNotConfigured"] = "Dropbox: not configured",
            ["DropboxNotConfigured"] = "Dropbox sync is not configured.",
            ["DropboxDisabled"] = "Dropbox auto-sync is disabled.",
            ["DropboxUpToDate"] = "Dropbox is up to date.",
            ["DropboxUploaded"] = "Database uploaded to Dropbox.",
            ["DropboxDownloaded"] =
                "Database downloaded from Dropbox. The previous local database was backed up to:\n{0}",
            ["DropboxConflict"] =
                "Both local and Dropbox databases have changed. Use Sync now to choose which one to keep.",
            ["DropboxError"] = "Dropbox sync failed: {0}",
            ["DropboxConflictTitle"] = "Dropbox conflict",
            ["DropboxConflictUpload"] =
                "Both local and Dropbox databases have changed. Upload the local database and overwrite Dropbox?",
            ["DropboxConflictDownload"] =
                "Download the Dropbox database and overwrite the local database?",
            ["DropboxSyncNow"] = "Sync now",
            ["DropboxSyncComplete"] = "Dropbox sync",
            ["DropboxPathInvalid"] = "Remote path must start with / and end with .db.",
            ["DropboxAppKey"] = "App key",
            ["DropboxAppSecret"] = "App secret (optional)",
            ["DropboxRedirectUri"] = "Redirect URI",
            ["DropboxConnect"] = "Connect to Dropbox",
            ["DropboxDisconnect"] = "Disconnect",
            ["DropboxConnecting"] = "Waiting for Dropbox authorization…",
            ["DropboxDisconnected"] = "Disconnected from Dropbox.",
            ["DropboxNotConnected"] = "Dropbox is not connected.",
            ["DropboxAppKeyRequired"] = "Enter the Dropbox app key.",
            ["DropboxAuthFailed"] = "Dropbox authorization failed: {0}",
            ["DropboxTokenHint"] = "Stored in the settings file.",
            ["ValidationChooseProject"] = "Choose a project for a work entry.",
            ["ValidationEndAfterStart"] = "End time must be later than start time.",
            ["ValidationTimesRange"] = "Times must be between 00:00 and 24:00.",
            ["ValidationAwayReason"] = "Enter a reason for the away time.",
            ["ValidationAwayReasonLength"] = "Away reason must be 80 characters or fewer.",
            ["ValidationAwayNoProject"] = "Away entries cannot have a project.",
            ["ValidationNonWorkNoProject"] = "{0} entries cannot have a project.",
            ["ValidationOverlap"] = "This {0} entry overlaps {1} time ({2}–{3}{4}).",
            ["ValidationXPauseWeekday"] = "X-Pause can only be used Monday to Friday.",
            ["ValidationXPauseStep"] = "X-Pause must be entered in 15-minute steps.",
            ["ValidationXPauseDayLimit"] = "X-Pause cannot be longer than 1:00 in one day.",
            ["ValidationXPauseWeekly"] =
                "X-Pause is limited to 1:00 per week. You have {0} remaining this week.",
            ["ValidationXPauseWeeklySimple"] = "X-Pause is limited to 1:00 per week.",
            ["ValidationXPauseInside"] =
                "X-Pause must be inside a work entry. It cannot start at the beginning " +
                "or end at the end of the workday.",
            ["ValidationXPauseOrphan"] =
                "X-Pause on {0} is no longer inside a work entry. " +
                "Adjust the X-Pause or the work entry first.",
            ["ValidationProjectMissing"] = "The selected project no longer exists.",
            ["ValidationProjectArchived"] =
                "The selected project is archived. Choose an active project.",
            ["ValidationProjectNameEmpty"] = "Enter a project name.",
            ["ValidationProjectNameLength"] = "Project names must be 80 characters or fewer.",
            ["ValidationProjectExists"] = "Project \"{0}\" already exists.",
            ["ValidationProjectNotFound"] = "Project not found.",
            ["ValidationProjectUsed"] =
                "This project is used by time entries. Archive it instead of deleting it.",
            ["ValidationEntryNotFound"] = "Entry not found.",
            ["TimeParse"] = "Enter a time as HH:MM using the 24-hour clock.",
            ["TimeEndOfDay"] = "24:00 can only be used as an end time.",
            ["TimeRange"] = "Time must be between 00:00 and 24:00.",
            ["DurationNegative"] = "Duration cannot be negative.",
            ["DateParse"] = "Enter a date as YYYY-MM-DD.",
        },
        ["sv"] = new Dictionary<string, string>
        {
            ["MenuFile"] = "Arkiv",
            ["MenuData"] = "Data",
            ["MenuHelp"] = "Hjälp",
            ["MenuDatabaseLocation"] = "Databasplats…",
            ["MenuBackup"] = "Säkerhetskopiera databas…",
            ["MenuRestore"] = "Återställ databas…",
            ["MenuExit"] = "Avsluta",
            ["MenuProjects"] = "Projekt…",
            ["MenuRefresh"] = "Uppdatera",
            ["MenuAbout"] = "Om TimeReport",
            ["HeaderSubtitle"] = "Lokal tidrapportering",
            ["ButtonProjects"] = "Projekt",
            ["ButtonDatabase"] = "Databas",
            ["ButtonBackup"] = "Säkerhetskopiera",
            ["ButtonRestore"] = "Återställ",
            ["ButtonAbout"] = "Om",
            ["TabTimesheet"] = "Tidrapport",
            ["TabTotals"] = "Summor",
            ["ButtonThisWeek"] = "Denna vecka",
            ["ButtonAddEntry"] = "+ Lägg till post",
            ["ButtonAdd"] = "+ Lägg till",
            ["ButtonEdit"] = "Redigera",
            ["ButtonDelete"] = "Ta bort",
            ["ButtonSave"] = "Spara",
            ["ButtonCancel"] = "Avbryt",
            ["ButtonClose"] = "Stäng",
            ["ButtonCurrent"] = "Aktuell",
            ["ButtonUseDefault"] = "Använd standard",
            ["ButtonBrowseFolder"] = "Bläddra mapp…",
            ["LabelWorked"] = "Arbetat",
            ["LabelLunch"] = "Lunch",
            ["LabelAway"] = "Frånvaro",
            ["LabelXPause"] = "X-Paus",
            ["LabelDaysWorked"] = "Arbetade dagar",
            ["LabelDaysAway"] = "Frånvarodagar",
            ["LabelType"] = "Typ",
            ["LabelProjectReason"] = "Projekt / orsak",
            ["LabelStart"] = "Start",
            ["LabelEnd"] = "Slut",
            ["LabelDuration"] = "Längd",
            ["LabelPeriod"] = "Period",
            ["PeriodWeek"] = "Vecka",
            ["PeriodMonth"] = "Månad",
            ["PeriodYear"] = "År",
            ["PeriodAllTime"] = "All tid",
            ["LabelToday"] = "Idag",
            ["EmptyEntries"] = "Inga poster ännu",
            ["StatusDatabase"] = "Databas:",
            ["StatusLocalOnly"] = "All data stannar på den här datorn",
            ["LabelWeekPrefix"] = "Vecka",
            ["LabelDate"] = "Datum",
            ["LabelTime"] = "Tid",
            ["Label24Hour"] = "24-timmarsklocka",
            ["LabelProject"] = "Projekt",
            ["LabelAwayReason"] = "Frånvaroorsak",
            ["LabelNewProject"] = "Ny…",
            ["EntryKindWork"] = "Arbete",
            ["EntryKindLunch"] = "Lunch",
            ["EntryKindAway"] = "Frånvaro",
            ["EntryKindXPause"] = "X-Paus",
            ["EntryDialogAddTitle"] = "Lägg till post",
            ["EntryDialogEditTitle"] = "Redigera post",
            ["EntryDialogInfoWork"] =
                "Arbetstid räknas. Lunch som överlappar arbete dras av; X-Paus räknas separat.",
            ["EntryDialogInfoLunch"] =
                "Lunch dras av från arbetstiden när den överlappar en arbetspost.",
            ["EntryDialogInfoAway"] =
                "Frånvaro räknas separat och räknas inte som arbete.",
            ["EntryDialogInfoXPause"] =
                "X-Paus är en separat veckoförmån. Den måste ligga inom arbetstiden, " +
                "i steg om 15 minuter, högst 1:00 per vecka, och dras inte av från arbetad tid.",
            ["EntryDialogXPauseHint"] =
                "X-Paus den här veckan: {0} använt av 1:00, {1} kvar.{2}",
            ["EntryDialogXPauseWeekdayNote"] = " X-Paus finns bara måndag–fredag.",
            ["EntryDialogInvalidDate"] =
                "Ange ett giltigt datum för att se veckans X-Paus-saldo.",
            ["EntryDialogChooseProject"] =
                "Välj ett projekt. Använd ”Ny…” för att skapa ett om det behövs.",
            ["ProjectsTitle"] = "Projekt",
            ["ProjectsHeaderProject"] = "Projekt",
            ["ProjectsHeaderStatus"] = "Status",
            ["ProjectsHeaderEntries"] = "Poster",
            ["ProjectStatusActive"] = "Aktiv",
            ["ProjectStatusArchived"] = "Arkiverad",
            ["ButtonRename"] = "Byt namn",
            ["ButtonArchive"] = "Arkivera",
            ["ButtonUnarchive"] = "Återställ",
            ["DatabaseLocationTitle"] = "Databasplats",
            ["CurrentDatabase"] = "Aktuell databas",
            ["NewDatabaseFile"] = "Ny databasfil",
            ["CopyCurrentDatabase"] = "Kopiera den aktuella databasen till den nya platsen",
            ["DatabaseLocationInfo"] =
                "Om den valda filen redan finns öppnas den i stället för att skrivas över. " +
                "Den aktuella databasfilen lämnas orörd.",
            ["StatusChooseDatabase"] = "Ange en giltig databassökväg.",
            ["StatusCurrentDatabase"] = "Detta är den aktuella databasen.",
            ["StatusExistingDatabase"] =
                "En befintlig TimeReport-databas öppnas. Den aktuella databasfilen lämnas orörd.",
            ["StatusNewDatabase"] = "En ny databas skapas på den här platsen.",
            ["StatusInvalidDatabase"] = "Den här filen kan inte användas: {0}",
            ["AboutDescription"] =
                "En lokal tidrapport. Inget konto, ingen molntjänst och ingen telemetri.",
            ["AboutDatabase"] = "Databas",
            ["AboutDataFolder"] = "Datamapp",
            ["AboutSettingsFile"] = "Inställningsfil",
            ["AboutSchemaVersion"] = "Schemaversion: {0}",
            ["AboutOpenDataFolder"] = "Öppna datamapp",
            ["MessageOK"] = "OK",
            ["MessageCancel"] = "Avbryt",
            ["MessageDeleteEntryTitle"] = "Ta bort post",
            ["MessageDeleteEntry"] = "Ta bort den här posten ({0})?",
            ["MessageCannotDeleteEntry"] = "Kan inte ta bort post",
            ["MessageCannotLoadEntries"] = "Kan inte läsa poster",
            ["MessageBackupComplete"] = "Säkerhetskopiering klar",
            ["MessageBackupSaved"] = "Säkerhetskopiering sparad till:\n{0}",
            ["MessageBackupFailed"] = "Säkerhetskopiering misslyckades",
            ["MessageRestoreTitle"] = "Återställ databas",
            ["MessageRestoreConfirm"] =
                "Återställning ersätter all aktuell data med den valda säkerhetskopian.\n\n" +
                "En säkerhetskopia av den aktuella databasen skapas automatiskt först.\n\n" +
                "Fortsätt?",
            ["MessageRestoreComplete"] = "Återställning klar",
            ["MessageRestoreDone"] =
                "Data återställd.\n\nDen tidigare databasen säkerhetskopierades till:\n{0}",
            ["MessageRestoreFailed"] = "Återställning misslyckades",
            ["MessageDatabaseLocationChanged"] = "Databasplats ändrad",
            ["MessageNowUsing"] = "TimeReport använder nu:\n{0}",
            ["MessageCannotChangeDatabase"] = "Kan inte ändra databasplats",
            ["MessageCannotCreateProject"] = "Kan inte skapa projekt",
            ["MessageCannotRenameProject"] = "Kan inte byta namn på projekt",
            ["MessageCannotChangeProject"] = "Kan inte ändra projekt",
            ["MessageCannotDeleteProject"] = "Kan inte ta bort projekt",
            ["MessageDeleteProjectTitle"] = "Ta bort projekt",
            ["MessageDeleteProject"] =
                "Ta bort \"{0}\"?\n\nProjekt med tidsposter kan bara arkiveras.",
            ["MessageCannotSaveEntry"] = "Kan inte spara post",
            ["MessageCannotUseDatabase"] = "Kan inte använda den här databasen",
            ["MessageCannotOpenDataFolder"] = "Kan inte öppna datamappen",
            ["MessageCannotOpenDropbox"] = "Kan inte öppna Dropbox App Console",
            ["NewProjectTitle"] = "Nytt projekt",
            ["ProjectNamePrompt"] = "Projektnamn:",
            ["RenameProjectTitle"] = "Byt namn på projekt",
            ["AboutTitle"] = "Om TimeReport",
            ["VersionLabel"] = "Version",
            ["MenuDropbox"] = "Dropbox-synk…",
            ["MenuSyncNow"] = "Synka nu",
            ["ButtonDropbox"] = "Dropbox",
            ["ButtonSync"] = "Synka",
            ["DropboxSettingsTitle"] = "Dropbox-synk",
            ["DropboxAccessToken"] = "Åtkomsttoken",
            ["DropboxRemotePath"] = "Fjärrsökväg",
            ["DropboxRemotePathHint"] = "Exempel: /TimeReport/timereport.db",
            ["DropboxAutoSync"] = "Synka automatiskt efter ändringar",
            ["DropboxInstructions"] =
                "Skapa en Dropbox-app med files.content.read, files.content.write och files.metadata.read. " +
                "Lägg till omdirigerings-URI:n nedan i appen och ange sedan appnyckeln och anslut.",
            ["DropboxLocalCopyInfo"] = "Din lokala databas är fortfarande arbetskopian. Dropbox används för synkronisering.",
            ["DropboxOpenAppConsole"] = "Öppna Dropbox App Console",
            ["DropboxTestConnection"] = "Testa anslutning",
            ["DropboxTesting"] = "Testar anslutning…",
            ["DropboxConnected"] = "Ansluten till Dropbox.",
            ["DropboxTokenRequired"] = "Ange en Dropbox-åtkomsttoken.",
            ["DropboxSaved"] = "Dropbox-inställningarna sparades.",
            ["DropboxStatusOff"] = "Dropbox: av",
            ["DropboxStatusReady"] = "Dropbox: klar",
            ["DropboxStatusSyncing"] = "Dropbox: synkar…",
            ["DropboxStatusSynced"] = "Dropbox: synkad",
            ["DropboxStatusUpToDate"] = "Dropbox: uppdaterad",
            ["DropboxStatusConflict"] = "Dropbox: konflikt",
            ["DropboxStatusError"] = "Dropbox: fel",
            ["DropboxStatusDisabled"] = "Dropbox: autosynk av",
            ["DropboxStatusNotConfigured"] = "Dropbox: inte konfigurerad",
            ["DropboxNotConfigured"] = "Dropbox-synk är inte konfigurerad.",
            ["DropboxDisabled"] = "Automatisk Dropbox-synk är avstängd.",
            ["DropboxUpToDate"] = "Dropbox är uppdaterad.",
            ["DropboxUploaded"] = "Databasen laddades upp till Dropbox.",
            ["DropboxDownloaded"] =
                "Databasen hämtades från Dropbox. Den tidigare lokala databasen säkerhetskopierades till:\n{0}",
            ["DropboxConflict"] =
                "Både den lokala databasen och Dropbox-databasen har ändrats. Använd Synka nu för att välja vilken som ska behållas.",
            ["DropboxError"] = "Dropbox-synken misslyckades: {0}",
            ["DropboxConflictTitle"] = "Dropbox-konflikt",
            ["DropboxConflictUpload"] =
                "Både den lokala databasen och Dropbox-databasen har ändrats. Ladda upp den lokala databasen och skriv över Dropbox?",
            ["DropboxConflictDownload"] =
                "Hämta Dropbox-databasen och skriv över den lokala databasen?",
            ["DropboxSyncNow"] = "Synka nu",
            ["DropboxSyncComplete"] = "Dropbox-synk",
            ["DropboxPathInvalid"] = "Fjärrsökvägen måste börja med / och sluta med .db.",
            ["DropboxAppKey"] = "App-nyckel",
            ["DropboxAppSecret"] = "App-hemlighet (valfri)",
            ["DropboxRedirectUri"] = "Omdirigerings-URI",
            ["DropboxConnect"] = "Anslut till Dropbox",
            ["DropboxDisconnect"] = "Koppla från",
            ["DropboxConnecting"] = "Väntar på Dropbox-auktorisering…",
            ["DropboxDisconnected"] = "Frånkopplad från Dropbox.",
            ["DropboxNotConnected"] = "Dropbox är inte ansluten.",
            ["DropboxAppKeyRequired"] = "Ange Dropbox-appnyckeln.",
            ["DropboxAuthFailed"] = "Dropbox-auktoriseringen misslyckades: {0}",
            ["DropboxTokenHint"] = "Sparas i inställningsfilen.",
            ["ValidationChooseProject"] = "Välj ett projekt för en arbetspost.",
            ["ValidationEndAfterStart"] = "Sluttiden måste vara senare än starttiden.",
            ["ValidationTimesRange"] = "Tiderna måste vara mellan 00:00 och 24:00.",
            ["ValidationAwayReason"] = "Ange en orsak till frånvaron.",
            ["ValidationAwayReasonLength"] = "Frånvaroorsaken får vara högst 80 tecken.",
            ["ValidationAwayNoProject"] = "Frånvaroposter kan inte ha ett projekt.",
            ["ValidationNonWorkNoProject"] = "{0}-poster kan inte ha ett projekt.",
            ["ValidationOverlap"] = "Den här posten ({0}) överlappar {1} ({2}–{3}{4}).",
            ["ValidationXPauseWeekday"] = "X-Paus kan bara användas måndag till fredag.",
            ["ValidationXPauseStep"] = "X-Paus måste anges i steg om 15 minuter.",
            ["ValidationXPauseDayLimit"] = "X-Paus får vara högst 1:00 per dag.",
            ["ValidationXPauseWeekly"] =
                "X-Paus är begränsad till 1:00 per vecka. Du har {0} kvar den här veckan.",
            ["ValidationXPauseWeeklySimple"] = "X-Paus är begränsad till 1:00 per vecka.",
            ["ValidationXPauseInside"] =
                "X-Paus måste ligga inom en arbetspost. Den kan inte börja i början " +
                "eller sluta i slutet av arbetsdagen.",
            ["ValidationXPauseOrphan"] =
                "X-Pausen {0} ligger inte längre inom en arbetspost. " +
                "Justera X-Pausen eller arbetsposten först.",
            ["ValidationProjectMissing"] = "Det valda projektet finns inte längre.",
            ["ValidationProjectArchived"] =
                "Det valda projektet är arkiverat. Välj ett aktivt projekt.",
            ["ValidationProjectNameEmpty"] = "Ange ett projektnamn.",
            ["ValidationProjectNameLength"] = "Projektnamn får vara högst 80 tecken.",
            ["ValidationProjectExists"] = "Projektet \"{0}\" finns redan.",
            ["ValidationProjectNotFound"] = "Projektet hittades inte.",
            ["ValidationProjectUsed"] =
                "Projektet används av tidsposter. Arkivera det i stället för att ta bort det.",
            ["ValidationEntryNotFound"] = "Posten hittades inte.",
            ["TimeParse"] = "Ange en tid som HH:MM med 24-timmarsklocka.",
            ["TimeEndOfDay"] = "24:00 kan bara användas som sluttid.",
            ["TimeRange"] = "Tiden måste vara mellan 00:00 och 24:00.",
            ["DurationNegative"] = "Längden kan inte vara negativ.",
            ["DateParse"] = "Ange ett datum som ÅÅÅÅ-MM-DD.",
        },
    };

    public static string CurrentLanguage { get; private set; } = "en";

    public static IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new("en", "English"),
        new("sv", "Svenska"),
    ];

    public static event Action? LanguageChanged;

    public static IReadOnlyCollection<string> Keys => Strings["en"].Keys;

    public static bool HasTranslation(string key, string language) =>
        Strings.TryGetValue(language, out var values) && values.ContainsKey(key);

    public static IReadOnlyList<string> WeekdayNames =>
        Weekdays.TryGetValue(CurrentLanguage, out var values) ? values : Weekdays["en"];

    public static IReadOnlyList<string> WeekdayShort =>
        WeekdaysShort.TryGetValue(CurrentLanguage, out var values) ? values : WeekdaysShort["en"];

    public static IReadOnlyList<string> MonthNames =>
        Months.TryGetValue(CurrentLanguage, out var values) ? values : Months["en"];

    public static void SetLanguage(string? code)
    {
        var normalized = string.Equals(code, "sv", StringComparison.OrdinalIgnoreCase) ? "sv" : "en";
        CurrentLanguage = normalized;

        if (Application.Current is not null)
        {
            foreach (var (key, value) in Strings[normalized])
            {
                Application.Current.Resources[key] = value;
            }
        }

        LanguageChanged?.Invoke();
    }

    public static string T(string key)
    {
        if (Strings.TryGetValue(CurrentLanguage, out var language) &&
            language.TryGetValue(key, out var value))
        {
            return value;
        }
        if (Strings["en"].TryGetValue(key, out var fallback))
        {
            return fallback;
        }
        return key;
    }

    public static string T(string key, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, T(key), args);
}

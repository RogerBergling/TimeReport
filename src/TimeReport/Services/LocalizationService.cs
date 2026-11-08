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
            ["MenuFile"] = "_File",
            ["MenuData"] = "_Data",
            ["MenuHelp"] = "_Help",
            ["MenuDatabaseLocation"] = "_Database location…",
            ["MenuBackup"] = "Backup database…",
            ["MenuBackupAction"] = "_Backup database…",
            ["MenuRestore"] = "Restore database…",
            ["MenuCheckDatabase"] = "Check database integrity…",
            ["MenuCompactDatabase"] = "Compact database…",
            ["MenuRepairDatabase"] = "Repair database…",
            ["MessageDatabaseCheck"] = "Database integrity",
            ["MessageDatabaseCheckResult"] = "SQLite integrity check: {0}",
            ["MessageCompactDatabase"] = "Compact database",
            ["MessageCompactDatabaseConfirm"] = "Compact the database now? A backup is not required.",
            ["MessageCompactDatabaseDone"] = "Database compaction completed.",
            ["MessageRepairDatabase"] = "Repair database",
            ["MessageRepairDatabaseConfirm"] = "Repair the database? A backup will be created first.",
            ["MessageRepairDatabaseDone"] = "Database repair completed. Backup: {0}",
            ["MenuVerifyBackup"] = "Verify backup…",
            ["MessageVerifyBackup"] = "Verify backup",
            ["MessageBackupValid"] = "The backup is valid. Schema version: {0}.",
            ["MessageBackupInvalid"] = "The backup is not valid:\n\n{0}",
            ["MenuRestoreAction"] = "_Restore database…",
            ["MenuExit"] = "E_xit",
            ["MenuEntryTypes"] = "_Entry types…",
            ["ButtonTypes"] = "Types",
            ["ButtonManageTypes"] = "Manage…",
            ["EntryTypesTitle"] = "Entry types",
            ["TypeHeaderName"] = "Name",
            ["TypeHeaderCategory"] = "Category",
            ["TypeHeaderStatus"] = "Status",
            ["TypeHeaderEntries"] = "Entries",
            ["NewTypeTitle"] = "New entry type",
            ["RenameTypeTitle"] = "Rename entry type",
            ["TypeNamePrompt"] = "Type name:",
            ["LabelCategory"] = "Category",
            ["ValidationTypeNameEmpty"] = "Enter a type name.",
            ["ValidationTypeNameLength"] = "Type names must be 80 characters or fewer.",
            ["ValidationTypeExists"] = "Type \"{0}\" already exists.",
            ["ValidationTypeNotFound"] = "Entry type not found.",
            ["ValidationTypeUsed"] = "This entry type is used by time entries. Archive it instead of deleting it.",
            ["ValidationTypeMissing"] = "Choose an entry type.",
            ["ValidationTypeArchived"] = "The selected entry type is archived. Choose an active type.",
            ["ValidationTypeCategory"] = "Choose a valid type category.",
            ["MessageCannotCreateType"] = "Cannot create entry type",
            ["MessageCannotRenameType"] = "Cannot rename entry type",
            ["MessageCannotChangeType"] = "Cannot change entry type",
            ["MessageCannotDeleteType"] = "Cannot delete entry type",
            ["MessageDeleteTypeTitle"] = "Delete entry type",
            ["MessageDeleteType"] = "Delete \"{0}\"?\n\nTypes used by time entries can only be archived.",
            ["MenuExportExcel"] = "_Export to Excel (.xlsx)…",
            ["ButtonExport"] = "Export",
            ["ExportDialogTitle"] = "Export to Excel",
            ["ExportFrom"] = "From date",
            ["ExportTo"] = "To date",
            ["ExportThisMonth"] = "This month",
            ["ExportLastMonth"] = "Last month",
            ["ExportThisYear"] = "This year",
            ["ExportLastYear"] = "Last year",
            ["ExportRangeInfo"] = "{0} – {1} · {2} days",
            ["ExportInvalidRange"] = "The start date must be before the end date.",
            ["ExportPathIsDatabase"] = "Choose a location other than the live database.",
            ["ExportNoData"] = "There is no data to export.",
            ["Exporting"] = "Exporting…",
            ["ExportSuccess"] = "Export complete",
            ["ExportSaved"] = "Excel file saved to:\n{0}",
            ["ExportFailed"] = "Export failed",
            ["ExportFileType"] = "Excel workbook",
            ["ExportSheetSummary"] = "Summary",
            ["ExportSheetEntries"] = "Entries",
            ["ExportSheetDaily"] = "Daily totals",
            ["ExportSheetMonthly"] = "Monthly totals",
            ["ExportSheetProjects"] = "Projects",
            ["ExportSheetTypes"] = "Types",
            ["ExportTitle"] = "TimeReport export",
            ["ExportGenerated"] = "Generated",
            ["ExportPeriod"] = "Period",
            ["ExportDays"] = "Days",
            ["ExportEntries"] = "Entries",
            ["ExportProjects"] = "Projects",
            ["ExportTypes"] = "Types",
            ["ExportDate"] = "Date",
            ["ExportWeekday"] = "Weekday",
            ["ExportType"] = "Type",
            ["ExportCategory"] = "Category",
            ["ExportProject"] = "Project",
            ["ExportAwayReason"] = "Away reason",
            ["ExportStart"] = "Start",
            ["ExportEnd"] = "End",
            ["ExportDuration"] = "Duration",
            ["ExportMonth"] = "Month",
            ["ExportStatus"] = "Status",
            ["ExportWorkHours"] = "Work hours",
            ["ExportTotalHours"] = "Total hours",
            ["ExportTimeZone"] = "Time zone",
            ["MenuNewEntry"] = "_New entry…",
            ["MenuProjects"] = "_Projects…",
            ["MenuRefresh"] = "_Refresh",
            ["MenuCheckUpdates"] = "_Check for updates…",
            ["UpdateTitle"] = "TimeReport updates",
            ["UpdateUpToDate"] = "You are using the latest version.",
            ["UpdateAvailable"] = "Version {0} is available. Open the release page?",
            ["UpdateCheckFailed"] = "Could not check for updates: {0}",
            ["MenuAbout"] = "_About TimeReport",
            ["MenuSettings"] = "_Settings…",
            ["SettingsTitle"] = "Settings",
            ["SettingsTimeZone"] = "Time zone",
            ["SettingsTheme"] = "Theme",
            ["SettingsOvertime"] = "Overtime",
            ["SettingsOvertimeDaily"] = "Daily threshold",
            ["SettingsOvertimeWeekly"] = "Weekly threshold",
            ["SettingsHoursUnit"] = "hours",
            ["SettingsOvertimeHint"] =
                "Overtime is time worked above the daily or weekly threshold. " +
                "Set both to 0 to disable overtime tracking.",
            ["SettingsBackup"] = "Backups",
            ["SettingsBackupRetention"] = "Keep automatic backups",
            ["SettingsBackupRetentionHint"] =
                "Pre-restore, pre-migration and pre-encryption backups older than this count are removed.",
            ["SettingsUpdates"] = "Updates",
            ["SettingsCheckForUpdates"] = "Check for updates on startup",
            ["SettingsInvalidOvertime"] = "Enter overtime thresholds between 0 and 24 hours per day or 168 hours per week.",
            ["SettingsInvalidRetention"] = "Enter a backup retention count between 1 and 365.",
            ["MenuTheme"] = "_Theme",
            ["ThemeSystem"] = "System",
            ["ThemeLight"] = "Light",
            ["ThemeDark"] = "Dark",
            ["ThemeHighContrast"] = "High contrast",
            ["HeaderSubtitle"] = "Local time reporting",
            ["ButtonProjects"] = "Projects",
            ["ButtonDatabase"] = "Database",
            ["ButtonBackup"] = "Backup",
            ["ButtonRestore"] = "Restore",
            ["ButtonAbout"] = "About",
            ["TabTimesheet"] = "Timesheet",
            ["TabTotals"] = "Totals",
            ["ButtonThisWeek"] = "This week",
            ["ButtonPreviousWeek"] = "Previous week (Ctrl+Left)",
            ["ButtonNextWeek"] = "Next week (Ctrl+Right)",
            ["ButtonAddEntry"] = "+ Add entry",
            ["ButtonAdd"] = "+ Add",
            ["ButtonEdit"] = "Edit",
            ["ButtonDelete"] = "Delete",
            ["ButtonUndoDelete"] = "Undo delete",
            ["MessageCannotUndoDelete"] = "Cannot undo delete",
            ["ButtonSave"] = "Save",
            ["ButtonCancel"] = "Cancel",
            ["ButtonClose"] = "Close",
            ["ButtonCurrent"] = "Current",
            ["ButtonUseDefault"] = "Use default",
            ["ButtonBrowseFolder"] = "Browse folder…",
            ["LabelLanguage"] = "Language",
            ["LabelWorked"] = "Worked",
            ["LabelLunch"] = "Lunch",
            ["LabelAway"] = "Away",
            ["LabelXPause"] = "X-Pause",
            ["LabelOvertime"] = "Overtime",
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
            ["StatusLocalOnly"] = "Local database",
            ["FileTypeSqliteDatabase"] = "SQLite database",
            ["DatabaseFolderPickerTitle"] = "Choose the database folder",
            ["LabelWeekPrefix"] = "Week",
            ["LabelDate"] = "Date",
            ["LabelTime"] = "Time",
            ["LabelTimeZone"] = "Time zone",
            ["Label24Hour"] = "24-hour clock",
            ["LabelProject"] = "Project",
            ["LabelAwayReason"] = "Away reason (optional)",
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
                "A local-first time report. No account or telemetry. " +
                "Optional Dropbox sync and update checks are the only network features.",
            ["AboutWarranty"] =
                "Free and open source under the MIT License. Provided as is, without warranty; use at your own risk.",
            ["AboutDatabase"] = "Database",
            ["AboutDataFolder"] = "Data folder",
            ["AboutSettingsFile"] = "Settings file",
            ["AboutLogFile"] = "Log file",
            ["AboutSchemaVersion"] = "Schema version: {0}",
            ["AboutOpenDataFolder"] = "Open data folder",
            ["AboutOpenLogFile"] = "Open log file",
            ["MessageCannotOpenLogFile"] = "Cannot open log file",
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
            ["MessageCannotSaveSettings"] = "Cannot save settings",
            ["MessageAlreadyRunning"] =
                "TimeReport is already running. Close the other window and try again.",
            ["MessageDropboxDisabledForNewDatabase"] =
                "Dropbox auto-sync was turned off for the newly selected database. " +
                "Open Dropbox sync settings when you are ready to connect it.",
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
            ["BuildLabel"] = "Build",
            ["MenuDropbox"] = "_Dropbox sync…",
            ["MenuSyncNow"] = "_Sync now",
            ["ButtonDropbox"] = "Dropbox",
            ["ButtonSync"] = "Sync",
            ["DropboxSettingsTitle"] = "Dropbox sync",
            ["DropboxAccessToken"] = "Access token",
            ["DropboxRemotePath"] = "Remote path",
            ["DropboxRemotePathHint"] = "Example: /TimeReport/timereport.db",
            ["DropboxEncryptSync"] = "Encrypt the database before uploading",
            ["DropboxDisableEncryptionTitle"] = "Disable sync encryption?",
            ["DropboxDisableEncryptionMessage"] =
                "This will upload a plaintext copy of your database to Dropbox. " +
                "Anyone with access to that Dropbox file can read your time data. Continue?",
            ["DropboxProxy"] = "HTTPS proxy",
            ["DropboxProxyHint"] = "Optional, for example http://proxy.example:8080",
            ["DropboxCaCertificate"] = "Custom CA certificate",
            ["DropboxCaCertificateHint"] = "Optional PEM/DER file for TLS inspection proxies.",
            ["DropboxSyncPassphrase"] = "Sync encryption passphrase",
            ["DropboxSyncPassphraseHint"] =
                "Use the same passphrase on every device that shares this Dropbox file. " +
                "Re-enter an existing passphrase once to upgrade to the salted version 3 " +
                "container. Leave it blank to keep the current key.",
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
            ["DropboxSyncKeyMissing"] = "Set a sync encryption passphrase in Dropbox settings first.",
            ["DropboxSyncPassphraseMissing"] =
                "The Dropbox file was encrypted with a sync passphrase. Enter the same " +
                "passphrase in Dropbox settings before downloading it.",
            ["DropboxPlaintextRejected"] =
                "The Dropbox file is not encrypted, but sync encryption is enabled. " +
                "Downloading plaintext is blocked. Disable sync encryption explicitly " +
                "in Dropbox settings only if you intend to use a legacy plaintext file.",
            ["DropboxPassphraseTooWeak"] =
                "Use a stronger sync passphrase: at least 12 characters and more variety. " +
                "A longer multi-word passphrase is recommended.",
            ["DropboxProxyInvalid"] =
                "Enter a valid absolute HTTP or HTTPS proxy URL, or leave the field blank.",
            ["DropboxProxyCredentials"] =
                "Do not put usernames or passwords in the proxy URL. Use a proxy that " +
                "authenticates the device another way, or leave the field blank.",
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
            ["DropboxConflictLocal"] = "Local database",
            ["DropboxConflictRemote"] = "Dropbox database",
            ["DropboxConflictModified"] = "Modified: {0:yyyy-MM-dd HH:mm:ss}",
            ["DropboxConflictKeepLocal"] = "Keep local",
            ["DropboxConflictKeepRemote"] = "Keep Dropbox",
            ["DropboxConflictCancel"] = "Cancel",
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
            ["ValidationDateRange"] = "The end date must not be before the start date.",
        },
        ["sv"] = new Dictionary<string, string>
        {
            ["MenuFile"] = "_Arkiv",
            ["MenuData"] = "_Data",
            ["MenuHelp"] = "_Hjälp",
            ["MenuDatabaseLocation"] = "_Databasplats…",
            ["MenuBackup"] = "Säkerhetskopiera databas…",
            ["MenuBackupAction"] = "_Säkerhetskopiera databas…",
            ["MenuRestore"] = "Återställ databas…",
            ["MenuCheckDatabase"] = "Kontrollera databasintegritet…",
            ["MenuCompactDatabase"] = "Komprimera databas…",
            ["MenuRepairDatabase"] = "Reparera databas…",
            ["MessageDatabaseCheck"] = "Databasintegritet",
            ["MessageDatabaseCheckResult"] = "SQLite-integritetskontroll: {0}",
            ["MessageCompactDatabase"] = "Komprimera databas",
            ["MessageCompactDatabaseConfirm"] = "Komprimera databasen nu? Ingen säkerhetskopia krävs.",
            ["MessageCompactDatabaseDone"] = "Databaskomprimeringen är klar.",
            ["MessageRepairDatabase"] = "Reparera databas",
            ["MessageRepairDatabaseConfirm"] = "Reparera databasen? En säkerhetskopia skapas först.",
            ["MessageRepairDatabaseDone"] = "Databasreparationen är klar. Säkerhetskopia: {0}",
            ["MenuVerifyBackup"] = "Verifiera säkerhetskopia…",
            ["MessageVerifyBackup"] = "Verifiera säkerhetskopia",
            ["MessageBackupValid"] = "Säkerhetskopian är giltig. Schemaversion: {0}.",
            ["MessageBackupInvalid"] = "Säkerhetskopian är inte giltig:\n\n{0}",
            ["MenuRestoreAction"] = "_Återställ databas…",
            ["MenuExit"] = "A_vsluta",
            ["MenuEntryTypes"] = "_Posttyper…",
            ["ButtonTypes"] = "Typer",
            ["ButtonManageTypes"] = "Hantera…",
            ["EntryTypesTitle"] = "Posttyper",
            ["TypeHeaderName"] = "Namn",
            ["TypeHeaderCategory"] = "Kategori",
            ["TypeHeaderStatus"] = "Status",
            ["TypeHeaderEntries"] = "Poster",
            ["NewTypeTitle"] = "Ny posttyp",
            ["RenameTypeTitle"] = "Byt namn på posttyp",
            ["TypeNamePrompt"] = "Typnamn:",
            ["LabelCategory"] = "Kategori",
            ["ValidationTypeNameEmpty"] = "Ange ett typnamn.",
            ["ValidationTypeNameLength"] = "Typnamn får vara högst 80 tecken.",
            ["ValidationTypeExists"] = "Typen \"{0}\" finns redan.",
            ["ValidationTypeNotFound"] = "Posttypen hittades inte.",
            ["ValidationTypeUsed"] = "Posttypen används av tidsposter. Arkivera den i stället för att ta bort den.",
            ["ValidationTypeMissing"] = "Välj en posttyp.",
            ["ValidationTypeArchived"] = "Den valda posttypen är arkiverad. Välj en aktiv typ.",
            ["ValidationTypeCategory"] = "Välj en giltig typkategori.",
            ["MessageCannotCreateType"] = "Kan inte skapa posttyp",
            ["MessageCannotRenameType"] = "Kan inte byta namn på posttyp",
            ["MessageCannotChangeType"] = "Kan inte ändra posttyp",
            ["MessageCannotDeleteType"] = "Kan inte ta bort posttyp",
            ["MessageDeleteTypeTitle"] = "Ta bort posttyp",
            ["MessageDeleteType"] = "Ta bort \"{0}\"?\n\nPosttyper som används av tidsposter kan bara arkiveras.",
            ["MenuExportExcel"] = "_Exportera till Excel (.xlsx)…",
            ["ButtonExport"] = "Exportera",
            ["ExportDialogTitle"] = "Exportera till Excel",
            ["ExportFrom"] = "Från datum",
            ["ExportTo"] = "Till datum",
            ["ExportThisMonth"] = "Denna månad",
            ["ExportLastMonth"] = "Förra månaden",
            ["ExportThisYear"] = "Detta år",
            ["ExportLastYear"] = "Förra året",
            ["ExportRangeInfo"] = "{0} – {1} · {2} dagar",
            ["ExportInvalidRange"] = "Startdatumet måste vara före slutdatumet.",
            ["ExportPathIsDatabase"] = "Välj en annan plats än den aktiva databasen.",
            ["ExportNoData"] = "Det finns inga data att exportera.",
            ["Exporting"] = "Exporterar…",
            ["ExportSuccess"] = "Exporten är klar",
            ["ExportSaved"] = "Excel-filen sparades till:\n{0}",
            ["ExportFailed"] = "Exporten misslyckades",
            ["ExportFileType"] = "Excel-arbetsbok",
            ["ExportSheetSummary"] = "Sammanfattning",
            ["ExportSheetEntries"] = "Poster",
            ["ExportSheetDaily"] = "Dagsummor",
            ["ExportSheetMonthly"] = "Månadssummor",
            ["ExportSheetProjects"] = "Projekt",
            ["ExportSheetTypes"] = "Typer",
            ["ExportTitle"] = "TimeReport-export",
            ["ExportGenerated"] = "Skapad",
            ["ExportPeriod"] = "Period",
            ["ExportDays"] = "Dagar",
            ["ExportEntries"] = "Poster",
            ["ExportProjects"] = "Projekt",
            ["ExportTypes"] = "Typer",
            ["ExportDate"] = "Datum",
            ["ExportWeekday"] = "Veckodag",
            ["ExportType"] = "Typ",
            ["ExportCategory"] = "Kategori",
            ["ExportProject"] = "Projekt",
            ["ExportAwayReason"] = "Frånvaroorsak",
            ["ExportStart"] = "Start",
            ["ExportEnd"] = "Slut",
            ["ExportDuration"] = "Längd",
            ["ExportMonth"] = "Månad",
            ["ExportStatus"] = "Status",
            ["ExportWorkHours"] = "Arbetstimmar",
            ["ExportTotalHours"] = "Totala timmar",
            ["ExportTimeZone"] = "Tidszon",
            ["MenuNewEntry"] = "_Ny post…",
            ["MenuProjects"] = "_Projekt…",
            ["MenuRefresh"] = "_Uppdatera",
            ["MenuCheckUpdates"] = "_Sök efter uppdateringar…",
            ["UpdateTitle"] = "TimeReport-uppdateringar",
            ["UpdateUpToDate"] = "Du använder den senaste versionen.",
            ["UpdateAvailable"] = "Version {0} finns. Öppna releasesidan?",
            ["UpdateCheckFailed"] = "Kunde inte söka efter uppdateringar: {0}",
            ["MenuAbout"] = "_Om TimeReport",
            ["MenuSettings"] = "_Inställningar…",
            ["SettingsTitle"] = "Inställningar",
            ["SettingsTimeZone"] = "Tidszon",
            ["SettingsTheme"] = "Tema",
            ["SettingsOvertime"] = "Övertid",
            ["SettingsOvertimeDaily"] = "Daglig gräns",
            ["SettingsOvertimeWeekly"] = "Veckogräns",
            ["SettingsHoursUnit"] = "timmar",
            ["SettingsOvertimeHint"] =
                "Övertid är arbetad tid över den dagliga eller veckovisa gränsen. " +
                "Sätt båda till 0 för att stänga av övertidsberäkning.",
            ["SettingsBackup"] = "Säkerhetskopior",
            ["SettingsBackupRetention"] = "Behåll automatiska säkerhetskopior",
            ["SettingsBackupRetentionHint"] =
                "Säkerhetskopior före återställning, migrering och kryptering som är äldre än detta antal tas bort.",
            ["SettingsUpdates"] = "Uppdateringar",
            ["SettingsCheckForUpdates"] = "Sök efter uppdateringar vid start",
            ["SettingsInvalidOvertime"] = "Ange övertidsgränser mellan 0 och 24 timmar per dag eller 168 timmar per vecka.",
            ["SettingsInvalidRetention"] = "Ange ett antal säkerhetskopior mellan 1 och 365.",
            ["MenuTheme"] = "_Tema",
            ["ThemeSystem"] = "System",
            ["ThemeLight"] = "Ljust",
            ["ThemeDark"] = "Mörkt",
            ["ThemeHighContrast"] = "Hög kontrast",
            ["HeaderSubtitle"] = "Lokal tidrapportering",
            ["ButtonProjects"] = "Projekt",
            ["ButtonDatabase"] = "Databas",
            ["ButtonBackup"] = "Säkerhetskopiera",
            ["ButtonRestore"] = "Återställ",
            ["ButtonAbout"] = "Om",
            ["TabTimesheet"] = "Tidrapport",
            ["TabTotals"] = "Summor",
            ["ButtonThisWeek"] = "Denna vecka",
            ["ButtonPreviousWeek"] = "Föregående vecka (Ctrl+Vänster)",
            ["ButtonNextWeek"] = "Nästa vecka (Ctrl+Höger)",
            ["ButtonAddEntry"] = "+ Lägg till post",
            ["ButtonAdd"] = "+ Lägg till",
            ["ButtonEdit"] = "Redigera",
            ["ButtonDelete"] = "Ta bort",
            ["ButtonUndoDelete"] = "Ångra borttagning",
            ["MessageCannotUndoDelete"] = "Kan inte ångra borttagning",
            ["ButtonSave"] = "Spara",
            ["ButtonCancel"] = "Avbryt",
            ["ButtonClose"] = "Stäng",
            ["ButtonCurrent"] = "Aktuell",
            ["ButtonUseDefault"] = "Använd standard",
            ["ButtonBrowseFolder"] = "Bläddra mapp…",
            ["LabelLanguage"] = "Språk",
            ["LabelWorked"] = "Arbetat",
            ["LabelLunch"] = "Lunch",
            ["LabelAway"] = "Frånvaro",
            ["LabelXPause"] = "X-Paus",
            ["LabelOvertime"] = "Övertid",
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
            ["StatusLocalOnly"] = "Lokal databas",
            ["FileTypeSqliteDatabase"] = "SQLite-databas",
            ["DatabaseFolderPickerTitle"] = "Välj databasmapp",
            ["LabelWeekPrefix"] = "Vecka",
            ["LabelDate"] = "Datum",
            ["LabelTime"] = "Tid",
            ["LabelTimeZone"] = "Tidszon",
            ["Label24Hour"] = "24-timmarsklocka",
            ["LabelProject"] = "Projekt",
            ["LabelAwayReason"] = "Frånvaroorsak (valfri)",
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
                "En lokal tidrapport. Inget konto och ingen telemetri. " +
                "Valfri Dropbox-synkronisering och uppdateringskontroller är de enda nätverksfunktionerna.",
            ["AboutWarranty"] =
                "Fri och öppen källkod under MIT-licensen. Tillhandahålls i befintligt skick, utan garanti; använd på egen risk.",
            ["AboutDatabase"] = "Databas",
            ["AboutDataFolder"] = "Datamapp",
            ["AboutSettingsFile"] = "Inställningsfil",
            ["AboutLogFile"] = "Loggfil",
            ["AboutSchemaVersion"] = "Schemaversion: {0}",
            ["AboutOpenDataFolder"] = "Öppna datamapp",
            ["AboutOpenLogFile"] = "Öppna loggfil",
            ["MessageCannotOpenLogFile"] = "Kan inte öppna loggfilen",
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
            ["MessageCannotSaveSettings"] = "Kan inte spara inställningar",
            ["MessageAlreadyRunning"] =
                "TimeReport körs redan. Stäng det andra fönstret och försök igen.",
            ["MessageDropboxDisabledForNewDatabase"] =
                "Automatisk Dropbox-synkronisering stängdes av för den nya databasen. " +
                "Öppna Dropbox-inställningarna när du vill ansluta den.",
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
            ["BuildLabel"] = "Bygge",
            ["MenuDropbox"] = "_Dropbox-synk…",
            ["MenuSyncNow"] = "_Synka nu",
            ["ButtonDropbox"] = "Dropbox",
            ["ButtonSync"] = "Synka",
            ["DropboxSettingsTitle"] = "Dropbox-synk",
            ["DropboxAccessToken"] = "Åtkomsttoken",
            ["DropboxRemotePath"] = "Fjärrsökväg",
            ["DropboxRemotePathHint"] = "Exempel: /TimeReport/timereport.db",
            ["DropboxEncryptSync"] = "Kryptera databasen före uppladdning",
            ["DropboxDisableEncryptionTitle"] = "Inaktivera synkroniseringskryptering?",
            ["DropboxDisableEncryptionMessage"] =
                "Detta laddar upp en okrypterad kopia av databasen till Dropbox. " +
                "Alla som har åtkomst till filen kan läsa din tidsdata. Fortsätt?",
            ["DropboxProxy"] = "HTTPS-proxy",
            ["DropboxProxyHint"] = "Valfri, till exempel http://proxy.example:8080",
            ["DropboxCaCertificate"] = "Anpassat CA-certifikat",
            ["DropboxCaCertificateHint"] = "Valfri PEM/DER-fil för TLS-inspektionsproxies.",
            ["DropboxSyncPassphrase"] = "Lösenfras för synkroniseringskryptering",
            ["DropboxSyncPassphraseHint"] =
                "Använd samma lösenfras på alla enheter som delar den här Dropbox-filen. " +
                "Ange en befintlig lösenfras igen för att uppgradera till den saltade " +
                "version 3-behållaren. Lämna fältet tomt för att behålla nuvarande nyckel.",
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
            ["DropboxSyncKeyMissing"] = "Ange en lösenfras för synkroniseringskryptering i Dropbox-inställningarna först.",
            ["DropboxSyncPassphraseMissing"] =
                "Dropbox-filen krypterades med en lösenfras. Ange samma lösenfras i " +
                "Dropbox-inställningarna innan du hämtar den.",
            ["DropboxPlaintextRejected"] =
                "Dropbox-filen är inte krypterad, men krypterad synkronisering är aktiverad. " +
                "Hämtning av klartext blockeras. Stäng av krypteringen uttryckligen i " +
                "Dropbox-inställningarna endast om du avser att använda en äldre klartextfil.",
            ["DropboxPassphraseTooWeak"] =
                "Använd en starkare lösenfras: minst 12 tecken och mer variation. " +
                "En längre lösenfras med flera ord rekommenderas.",
            ["DropboxProxyInvalid"] =
                "Ange en giltig absolut HTTP- eller HTTPS-proxyadress, eller lämna fältet tomt.",
            ["DropboxProxyCredentials"] =
                "Lägg inte användarnamn eller lösenord i proxyadressen. Använd en proxy " +
                "som autentiserar enheten på annat sätt, eller lämna fältet tomt.",
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
            ["DropboxConflictLocal"] = "Lokal databas",
            ["DropboxConflictRemote"] = "Dropbox-databas",
            ["DropboxConflictModified"] = "Ändrad: {0:yyyy-MM-dd HH:mm:ss}",
            ["DropboxConflictKeepLocal"] = "Behåll lokal",
            ["DropboxConflictKeepRemote"] = "Behåll Dropbox",
            ["DropboxConflictCancel"] = "Avbryt",
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
            ["ValidationDateRange"] = "Slutdatumet får inte vara före startdatumet.",
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

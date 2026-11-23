using System.Globalization;
using TimeReport.Services;

namespace TimeReport.Web.Localization;

/// <summary>
/// Web-only UI text. English source strings are the keys; Swedish entries are
/// optional and English is used as the fallback. Shared desktop/web labels keep
/// using LocalizationService.
/// </summary>
public static class WebText
{
    private static readonly Dictionary<string, string> Swedish = new(StringComparer.Ordinal)
    {
        // Layout and navigation
        ["TimeReport home"] = "TimeReport start",
        ["Theme"] = "Tema",
        ["Matrix"] = "Matrix",
        ["Security"] = "Säkerhet",
        ["Audit"] = "Granskning",
        ["Fake data only · resets every {0} hour(s) · guest/guest + TOTP"] =
            "Endast falska data · återställs var {0}:e timme · gäst/gäst + TOTP",
        ["Time, without the busywork"] = "Tid, utan krångel",
        ["Skip to content"] = "Hoppa till innehåll",
        ["Main"] = "Huvudmeny",
        ["Mobile"] = "Mobilmeny",
        ["Switch theme"] = "Byt tema",
        ["Toggle matrix background"] = "Växla matrixbakgrund",
        ["Open menu"] = "Öppna meny",
        ["Settings"] = "Inställningar",
        ["Sign out"] = "Logga ut",
        ["Timesheet"] = "Tidrapport",
        ["Totals"] = "Summor",
        ["Projects"] = "Projekt",
        ["Types"] = "Typer",
        ["Export"] = "Exportera",
        ["Users"] = "Användare",
        ["Audit log"] = "Granskningslogg",
        ["About"] = "Om",
        ["Private time reporting. Every account has its own encrypted database. No telemetry, no third-party requests."] =
            "Privat tidrapportering. Varje konto har sin egen krypterade databas. Ingen telemetri, inga tredjepartsförfrågningar.",
        ["Source"] = "Källkod",
        ["Report a vulnerability"] = "Rapportera en sårbarhet",
        ["Account"] = "Konto",
        ["Two-factor verification"] = "Tvåfaktorsverifiering",
        ["First-run setup"] = "Första installationen",
        ["Show password"] = "Visa lösenord",
        ["QR code for the TimeReport demo guest"] = "QR-kod för TimeReport-demo-gästen",
        ["New name for {0}"] = "Nytt namn för {0}",
        ["e.g. Internal tools"] = "t.ex. Interna verktyg",
        ["e.g. Meeting"] = "t.ex. Möte",
        ["{0} project(s)"] = "{0} projekt",
        ["Created"] = "Skapad",
        ["Version"] = "Version",
        ["build"] = "bygge",
        ["commit"] = "incheckning",
        ["About TimeReport"] = "Om TimeReport",
        ["Private time reporting"] = "Privat tidrapportering",

        // Auth layout
        ["per-user encrypted databases"] = "krypterade databaser per användare",
        ["TOTP required for every account"] = "TOTP krävs för varje konto",
        ["recovery codes, hashed and single-use"] = "återställningskoder, hashade och engångskoder",
        ["no telemetry, no analytics"] = "ingen telemetri, ingen analys",
        ["no third-party browser requests"] = "inga tredjepartsförfrågningar från webbläsaren",
        ["TLS is terminated by your reverse proxy. This app binds to a private address and never exposes the database key to the browser."] =
            "TLS avslutas i din omvända proxy. Appen lyssnar på en privat adress och lämnar aldrig ut databasnyckeln till webbläsaren.",

        // Login and MFA
        ["Sign in"] = "Logga in",
        ["Use the account created for you by an administrator. Two-factor authentication is required."] =
            "Använd kontot som en administratör har skapat åt dig. Tvåfaktorsautentisering krävs.",
        ["Username"] = "Användarnamn",
        ["Password"] = "Lösenord",
        ["Show"] = "Visa",
        ["Accounts are created by an administrator."] = "Konton skapas av en administratör.",
        ["Verify it's you"] = "Verifiera att det är du",
        ["Enter the 6-digit code from your authenticator app for"] =
            "Ange den 6-siffriga koden från autentiseringsappen för",
        ["Authenticator code"] = "Autentiseringskod",
        ["Use a recovery code"] = "Använd en återställningskod",
        ["Verify"] = "Verifiera",
        ["Codes change every 30 seconds."] = "Koder ändras var 30:e sekund.",
        ["Back to sign in"] = "Tillbaka till inloggning",
        ["Verification expired"] = "Verifieringen har gått ut",
        ["Start again from the sign-in page."] = "Börja om från inloggningssidan.",
        ["Enter one of the single-use recovery codes you saved when you enabled two-factor authentication."] =
            "Ange en av engångskoderna som du sparade när du aktiverade tvåfaktorsautentisering.",
        ["Recovery code"] = "Återställningskod",
        ["Each code can be used once. Regenerate unused codes in Security after signing in."] =
            "Varje kod kan användas en gång. Skapa nya oanvända koder under Säkerhet efter inloggning.",
        ["Use an authenticator code"] = "Använd en autentiseringskod",
        ["Recovery codes are hashed at rest."] = "Återställningskoder lagras hashade.",

        // Setup
        ["Create the first administrator"] = "Skapa den första administratören",
        ["This page is available only while no accounts exist. The setup token is printed once in the server log at startup (or supplied with TIMEREPORT_SETUP_TOKEN)."] =
            "Den här sidan finns bara medan inga konton existerar. Installations-token skrivs en gång i serverloggen vid start (eller anges med TIMEREPORT_SETUP_TOKEN).",
        ["Setup token"] = "Installationstoken",
        ["Admin username"] = "Administratörens användarnamn",
        ["3–64 characters: letters, digits, dot, underscore or hyphen."] =
            "3–64 tecken: bokstäver, siffror, punkt, understreck eller bindestreck.",
        ["Display name (optional)"] = "Visningsnamn (valfritt)",
        ["At least 12 characters. A long passphrase is best."] =
            "Minst 12 tecken. En lång lösenfras är bäst.",
        ["Confirm password"] = "Bekräfta lösenord",
        ["Next: scan a QR code to enable TOTP."] = "Nästa steg: skanna en QR-kod för att aktivera TOTP.",
        ["Create administrator"] = "Skapa administratör",
        ["Accounts are never self-registered."] = "Konton registreras aldrig själva.",

        // Demo
        ["Demo"] = "Demo",
        ["Guest access"] = "Gäståtkomst",
        ["Fake data only. The demo resets every {0} hour(s). The guest account is an administrator so every page can be explored. The TOTP code changes every 30 seconds; the page refreshes it automatically."] =
            "Endast falska data. Demon återställs var {0}:e timme. Gästkontot är administratör så att alla sidor kan utforskas. TOTP-koden ändras var 30:e sekund; sidan uppdaterar den automatiskt.",
        ["Current TOTP"] = "Aktuell TOTP",
        ["TOTP secret:"] = "TOTP-hemlighet:",
        ["Fill guest credentials"] = "Fyll i gästuppgifterna",
        ["Copy current code"] = "Kopiera aktuell kod",
        ["Copy TOTP secret"] = "Kopiera TOTP-hemlighet",
        ["Show authenticator QR code"] = "Visa QR-kod för autentiseringsapp",
        ["Recovery code is one-time. If it has already been used, wait for the next demo reset or log in with the TOTP code."] =
            "Återställningskoden är en engångskod. Om den redan använts, vänta på nästa demoåterställning eller logga in med TOTP-koden.",

        // Timesheet
        ["Your week"] = "Din vecka",
        ["Work, lunch, away and X-Pause for the selected Monday–Sunday week. Week starts Monday; times use the 24-hour clock."] =
            "Arbete, lunch, frånvaro och X-Paus för den valda måndag–söndag-veckan. Veckan börjar på måndag; tiderna använder 24-timmarsklocka.",
        ["Add entry"] = "Lägg till post",
        ["Previous week"] = "Föregående vecka",
        ["Next week"] = "Nästa vecka",
        ["This week"] = "Denna vecka",
        ["Open totals"] = "Öppna summor",
        ["Today"] = "Idag",
        ["Add"] = "Lägg till",
        ["No entries yet."] = "Inga poster ännu.",
        ["Type"] = "Typ",
        ["Project / reason"] = "Projekt / orsak",
        ["Start"] = "Start",
        ["End"] = "Slut",
        ["Duration"] = "Längd",
        ["Actions"] = "Åtgärder",
        ["Edit"] = "Redigera",
        ["Delete"] = "Ta bort",
        ["Delete this entry?"] = "Ta bort den här posten?",

        // Entry form
        ["Edit entry"] = "Redigera post",
        ["Work entries need a project. Away entries need a reason. X-Pause must sit inside a work entry."] =
            "Arbetsposter behöver ett projekt. Frånvaroposter behöver en orsak. X-Paus måste ligga inom en arbetspost.",
        ["Back to week"] = "Tillbaka till veckan",
        ["Entry details"] = "Postens uppgifter",
        ["Times use the 24-hour clock. A work entry must end on the same date."] =
            "Tiderna använder 24-timmarsklocka. En arbetspost måste sluta samma datum.",
        ["— choose a project —"] = "— välj ett projekt —",
        ["No projects yet. Create one first."] = "Inga projekt ännu. Skapa ett först.",
        ["Create one first"] = "Skapa ett först",
        ["Reason for the away time"] = "Orsak till frånvaron",
        ["Cancel"] = "Avbryt",
        ["Save"] = "Spara",
        ["Delete entry"] = "Ta bort post",
        ["Rules"] = "Regler",
        ["Validation is transactional and runs on the server."] = "Valideringen är transaktionell och körs på servern.",
        ["Work cannot overlap work or away time."] = "Arbete kan inte överlappa arbete eller frånvaro.",
        ["Lunch may overlap work and is deducted from worked time."] =
            "Lunch får överlappa arbete och dras av från arbetad tid.",
        ["Away time is tracked separately and needs a reason."] =
            "Frånvaro spåras separat och behöver en orsak.",
        ["X-Pause is Monday–Friday only, in 15-minute steps, inside work hours, max 1:00 per ISO week."] =
            "X-Paus gäller endast måndag–fredag, i steg om 15 minuter, inom arbetstid, högst 1:00 per ISO-vecka.",
        ["Archived projects and types remain valid on historical entries only."] =
            "Arkiverade projekt och typer gäller endast historiska poster.",

        // Totals
        ["Time in perspective"] = "Tid i perspektiv",
        ["Daily, weekly, monthly, yearly and all-time totals for your private data."] =
            "Dagliga, veckovisa, månatliga, årliga och totala summor för dina privata data.",
        ["Period"] = "Period",
        ["Previous period"] = "Föregående period",
        ["Next period"] = "Nästa period",
        ["Current"] = "Aktuell",
        ["Breakdown"] = "Uppdelning",
        ["Rows follow the selected period."] = "Raderna följer den valda perioden.",
        ["No data in this period"] = "Inga data i den här perioden",
        ["Add an entry to start building totals."] = "Lägg till en post för att börja bygga summor.",
        ["Total"] = "Totalt",

        // Projects
        ["Data"] = "Data",
        ["Work entries need a project. Archive projects instead of deleting ones that already have entries."] =
            "Arbetsposter behöver ett projekt. Arkivera projekt i stället för att ta bort sådana som redan har poster.",
        ["Create project"] = "Skapa projekt",
        ["Names are unique for your account, up to 80 characters."] =
            "Namnen är unika för ditt konto, upp till 80 tecken.",
        ["Project name"] = "Projektnamn",
        ["Create"] = "Skapa",
        ["All projects"] = "Alla projekt",
        ["No projects yet"] = "Inga projekt ännu",
        ["Create a project above before adding a work entry."] =
            "Skapa ett projekt ovan innan du lägger till en arbetspost.",
        ["Project"] = "Projekt",
        ["Status"] = "Status",
        ["Entries"] = "Poster",
        ["Rename"] = "Byt namn",
        ["Active"] = "Aktiv",
        ["Archived"] = "Arkiverad",
        ["Restore"] = "Återställ",
        ["Archive"] = "Arkivera",
        ["Delete this project?"] = "Ta bort det här projektet?",

        // Types
        ["Entry types"] = "Posttyper",
        ["Work, Lunch, Away and X-Pause are built in. Add your own types with the same behaviour."] =
            "Arbete, Lunch, Frånvaro och X-Paus finns inbyggda. Lägg till egna typer med samma beteende.",
        ["Create type"] = "Skapa typ",
        ["Choose how the type behaves. The category cannot be changed later."] =
            "Välj hur typen beter sig. Kategorin kan inte ändras senare.",
        ["Type name"] = "Typnamn",
        ["Category"] = "Kategori",
        ["All types"] = "Alla typer",
        ["Types used by entries can be archived, not deleted."] =
            "Typer som används av poster kan arkiveras, inte tas bort.",
        ["Delete this entry type?"] = "Ta bort den här posttypen?",

        // Export
        ["Export to Excel"] = "Exportera till Excel",
        ["The workbook contains Summary, Entries, Daily totals, Monthly totals, Projects and Types sheets."] =
            "Arbetsboken innehåller bladen Sammanfattning, Poster, Dagsummor, Månadssummor, Projekt och Typer.",
        ["Custom range"] = "Eget intervall",
        ["Only your own entries are included."] = "Endast dina egna poster ingår.",
        ["From date"] = "Från datum",
        ["To date"] = "Till datum",
        ["Download .xlsx"] = "Ladda ner .xlsx",
        ["Any number of months. Times use the 24-hour clock."] =
            "Valfritt antal månader. Tiderna använder 24-timmarsklocka.",
        ["Quick ranges"] = "Snabba intervall",
        ["Presets are calculated in your selected time zone."] =
            "Snabbvalen beräknas i din valda tidszon.",
        ["This month"] = "Denna månad",
        ["Last month"] = "Förra månaden",
        ["This year"] = "Detta år",
        ["Last year"] = "Förra året",
        ["All time"] = "All tid",
        ["The export is generated on the server and served directly to this browser. No file is left behind."] =
            "Exporten skapas på servern och levereras direkt till webbläsaren. Ingen fil lämnas kvar.",

        // Admin users
        ["Administration"] = "Administration",
        ["Create accounts, reset credentials and manage roles. The admin UI does not open another user's timesheet. Credential resets are audited and can grant account access."] =
            "Skapa konton, återställ inloggningsuppgifter och hantera roller. Admin-gränssnittet öppnar inte någon annan användares tidrapport. Återställningar loggas och kan ge åtkomst till kontot.",
        ["Temporary password for {0}."] = "Tillfälligt lösenord för {0}.",
        ["Show this once to the user. They must change it at first sign-in."] =
            "Visa detta en gång för användaren. De måste byta det vid första inloggningen.",
        ["Copy password"] = "Kopiera lösenord",
        ["Create user"] = "Skapa användare",
        ["Leave the password blank to generate a temporary password."] =
            "Lämna lösenordet tomt för att skapa ett tillfälligt lösenord.",
        ["Initial password (optional)"] = "Startlösenord (valfritt)",
        ["At least 12 characters if supplied."] = "Minst 12 tecken om det anges.",
        ["Role"] = "Roll",
        ["Administrator"] = "Administratör",
        ["New users must change their password and enroll TOTP."] =
            "Nya användare måste byta lösenord och aktivera TOTP.",
        ["Accounts"] = "Konton",
        ["User"] = "Användare",
        ["MFA"] = "MFA",
        ["Enabled"] = "Aktiverad",
        ["Setup pending"] = "Konfiguration väntar",
        ["Disabled"] = "Inaktiverad",
        ["Manage"] = "Hantera",
        ["The demo guest has fixed credentials. Use another account to test resets."] =
            "Demo-gästen har fasta inloggningsuppgifter. Använd ett annat konto för att testa återställningar.",
        ["Reset password"] = "Återställ lösenord",
        ["Reset MFA"] = "Återställ MFA",
        ["Remove admin"] = "Ta bort admin",
        ["Make admin"] = "Gör till admin",
        ["Enable"] = "Aktivera",
        ["Disable"] = "Inaktivera",
        ["Delete user"] = "Ta bort användare",
        ["Type {0} to delete"] = "Ange {0} för att ta bort",
        ["Delete this user and their private database?"] = "Ta bort användaren och deras privata databas?",
        ["Reset this user's password?"] = "Återställ användarens lösenord?",
        ["Reset this user's MFA and recovery codes?"] = "Återställ användarens MFA och återställningskoder?",
        ["Remove administrator role?"] = "Ta bort administratörsrollen?",
        ["Grant administrator role?"] = "Ge administratörsrollen?",
        ["Enable this account?"] = "Aktivera det här kontot?",
        ["Disable this account and revoke sessions?"] = "Inaktivera kontot och återkalla sessionerna?",

        // Audit
        ["Authentication and account-management events. Passwords, codes and secrets are never logged."] =
            "Inloggnings- och kontohanteringshändelser. Lösenord, koder och hemligheter loggas aldrig.",
        ["Recent events"] = "Senaste händelserna",
        ["Newest first. The database keeps the latest 10,000 events."] =
            "Nyast först. Databasen behåller de senaste 10 000 händelserna.",
        ["Time (UTC)"] = "Tid (UTC)",
        ["Actor"] = "Aktör",
        ["Action"] = "Åtgärd",
        ["Detail"] = "Detalj",
        ["IP"] = "IP",
        ["No events recorded yet."] = "Inga händelser registrerade ännu.",

        // Account password
        ["Choose a new password"] = "Välj ett nytt lösenord",
        ["Change password"] = "Byt lösenord",
        ["You are using a temporary password. Choose your own password before continuing."] =
            "Du använder ett tillfälligt lösenord. Välj ett eget lösenord innan du fortsätter.",
        ["Changing your password signs out other browser sessions."] =
            "När du byter lösenord loggas andra webbläsarsessioner ut.",
        ["Use at least 12 characters. A long passphrase is best."] =
            "Använd minst 12 tecken. En lång lösenfras är bäst.",
        ["Current password"] = "Nuvarande lösenord",
        ["New password"] = "Nytt lösenord",
        ["Confirm new password"] = "Bekräfta nytt lösenord",
        ["Save password"] = "Spara lösenord",
        ["Good to know"] = "Bra att veta",
        ["Security details for your account."] = "Säkerhetsdetaljer för ditt konto.",
        ["Current-password re-authentication is required."] = "Nuvarande lösenord krävs igen.",
        ["Other sessions are revoked when the password changes."] =
            "Andra sessioner återkallas när lösenordet ändras.",
        ["Administrators can reset a password, but they never see it."] =
            "Administratörer kan återställa ett lösenord men ser det aldrig.",
        ["Demo guest credentials are fixed."] = "Demo-gästens inloggningsuppgifter är fasta.",
        ["Create another user under Users to test password changes."] =
            "Skapa en annan användare under Användare för att testa lösenordsbyten.",

        // Account TOTP
        ["Two-factor authentication"] = "Tvåfaktorsautentisering",
        ["TOTP is mandatory. Add TimeReport to an authenticator app, then enter a code to finish setup."] =
            "TOTP är obligatoriskt. Lägg till TimeReport i en autentiseringsapp och ange sedan en kod för att slutföra.",
        ["Two-factor authentication is enabled."] = "Tvåfaktorsautentisering är aktiverad.",
        ["Your account is protected with an authenticator app."] =
            "Ditt konto skyddas med en autentiseringsapp.",
        ["Security overview"] = "Säkerhetsöversikt",
        ["Recovery codes"] = "Återställningskoder",
        ["Scan the QR code"] = "Skanna QR-koden",
        ["Use Google Authenticator, Microsoft Authenticator, 1Password, Bitwarden or any TOTP app."] =
            "Använd Google Authenticator, Microsoft Authenticator, 1Password, Bitwarden eller valfri TOTP-app.",
        ["Can't scan it? Enter this secret manually:"] =
            "Kan du inte skanna? Ange hemligheten manuellt:",
        ["Copy secret"] = "Kopiera hemlighet",
        ["Enter the 6-digit code"] = "Ange den 6-siffriga koden",
        ["The code changes every 30 seconds. Make sure the device clock is correct."] =
            "Koden ändras var 30:e sekund. Kontrollera att enhetens klocka är rätt.",
        ["Enable TOTP"] = "Aktivera TOTP",
        ["Why this is required"] = "Varför detta krävs",
        ["Your time data is private; a password alone is not enough."] =
            "Din tidsdata är privat; ett lösenord räcker inte.",
        ["Every account must confirm TOTP before opening timesheets."] =
            "Varje konto måste bekräfta TOTP innan tidrapporten öppnas.",
        ["Recovery codes are single-use and stored only as keyed hashes."] =
            "Återställningskoder är engångskoder och lagras endast som nyckelhashar.",
        ["An administrator must reset MFA if your authenticator is lost."] =
            "En administratör måste återställa MFA om din autentiseringsapp försvinner.",
        ["Keep a copy of your recovery codes somewhere offline."] =
            "Förvara en kopia av återställningskoderna offline.",
        ["Re-authentication is required to enable TOTP."] = "Du måste autentisera igen för att aktivera TOTP.",
        ["Recovery codes are generated after verification."] = "Återställningskoder skapas efter verifieringen.",

        // Recovery codes
        ["Recovery codes let you sign in when your authenticator app is unavailable. Each code works once."] =
            "Återställningskoder låter dig logga in när autentiseringsappen inte är tillgänglig. Varje kod fungerar en gång.",
        ["Recovery codes are required."] = "Återställningskoder krävs.",
        ["Generate a set before using the application. If you lose your authenticator and have no codes, an administrator must reset your MFA."] =
            "Skapa en uppsättning innan du använder programmet. Om du förlorar autentiseringsappen och saknar koder måste en administratör återställa din MFA.",
        ["Save these codes now"] = "Spara dessa koder nu",
        ["They are not shown again. Store them offline, not in the same place as your password."] =
            "De visas inte igen. Förvara dem offline, inte på samma ställe som lösenordet.",
        ["Shown once"] = "Visas en gång",
        ["Print / save as PDF"] = "Skriv ut / spara som PDF",
        ["I saved them"] = "Jag har sparat dem",
        ["Printing uses your browser's print dialog. Choose \"Save as PDF\" to keep a copy."] =
            "Utskrift använder webbläsarens utskriftsdialog. Välj \"Spara som PDF\" för att behålla en kopia.",
        ["Generate a new set"] = "Skapa en ny uppsättning",
        ["Generating a new set invalidates every unused code from the previous set."] =
            "En ny uppsättning ogiltigförklarar alla oanvända koder från den tidigare uppsättningen.",
        ["remaining"] = "kvar",
        ["Re-authentication is required to replace recovery codes."] =
            "Du måste autentisera igen för att ersätta återställningskoder.",
        ["Ten codes are generated each time."] = "Tio koder skapas varje gång.",
        ["Generate codes"] = "Skapa koder",
        ["How recovery codes work"] = "Så fungerar återställningskoder",
        ["Useful when your phone is lost, replaced or wiped."] =
            "Användbart när telefonen tappas bort, byts ut eller rensas.",
        ["Each code can be used exactly once."] = "Varje kod kan användas exakt en gång.",
        ["Codes are stored as keyed hashes, never as plaintext."] =
            "Koder lagras som nyckelhashar, aldrig i klartext.",
        ["When you use a code, it is marked used immediately."] =
            "När du använder en kod markeras den omedelbart som använd.",
        ["Generating replacements removes all old unused codes."] =
            "När du skapar nya koder tas alla gamla oanvända koder bort.",
        ["Demo guest recovery codes are fixed."] = "Demo-gästens återställningskoder är fasta.",
        ["Use {0} on the sign-in page. Create another user to test credential changes and recovery-code regeneration."] =
            "Använd {0} på inloggningssidan. Skapa en annan användare för att testa inloggningsändringar och nya återställningskoder.",

        // Security
        ["Your sign-in, MFA and recovery settings."] = "Dina inställningar för inloggning, MFA och återställning.",
        ["Account protection"] = "Kontoskydd",
        ["All changes require your current password."] = "Alla ändringar kräver ditt nuvarande lösenord.",
        ["Manage recovery codes"] = "Hantera återställningskoder",
        ["Authenticator status"] = "Status för autentiseringsapp",
        ["TOTP cannot be disabled from the application. If you lose your authenticator, use a recovery code or ask an administrator to reset MFA."] =
            "TOTP kan inte stängas av i programmet. Om du förlorar autentiseringsappen använder du en återställningskod eller ber en administratör återställa MFA.",
        ["Recent security events"] = "Senaste säkerhetshändelserna",
        ["Sign-ins and account changes for this account."] = "Inloggningar och kontoändringar för detta konto.",
        ["Time"] = "Tid",
        ["Demo guest."] = "Demo-gäst.",
        ["The guest has administrator access with fixed credentials. Create another user to test password, MFA and recovery-code changes. Everything resets every {0} hour(s)."] =
            "Gästen har administratörsåtkomst med fasta inloggningsuppgifter. Skapa en annan användare för att testa lösenord, MFA och återställningskoder. Allt återställs var {0}:e timme.",

        // Settings
        ["Time zone, overtime thresholds and backup retention. Week starts Monday; times use the 24-hour clock."] =
            "Tidszon, övertidsgränser och säkerhetskopiering. Veckan börjar på måndag; tiderna använder 24-timmarsklocka.",
        ["Profile"] = "Profil",
        ["Your username is permanent. The display name can be changed."] =
            "Ditt användarnamn är permanent. Visningsnamnet kan ändras.",
        ["Display name"] = "Visningsnamn",
        ["Language"] = "Språk",
        ["Time zone"] = "Tidszon",
        ["Controls \"today\" and the default dates for new entries."] =
            "Styr \"idag\" och standarddatumen för nya poster.",
        ["Overtime"] = "Övertid",
        ["Set a daily threshold, a weekly threshold, or both. Use 0 to disable."] =
            "Ange en daglig gräns, en veckogräns eller båda. Använd 0 för att stänga av.",
        ["Daily threshold (hours)"] = "Daglig gräns (timmar)",
        ["Weekly threshold (hours)"] = "Veckogräns (timmar)",
        ["Automatic backup retention"] = "Behåll automatiska säkerhetskopior",
        ["Used by automatic pre-restore, pre-migration and pre-repair backups."] =
            "Används av automatiska säkerhetskopior före återställning, migrering och reparation.",
        ["Save settings"] = "Spara inställningar",

        // Errors
        ["Something went wrong"] = "Något gick fel",
        ["The request could not be completed. Try again, or check the server log."] =
            "Begäran kunde inte slutföras. Försök igen eller kontrollera serverloggen.",
        ["Bad request"] = "Felaktig begäran",
        ["The request was not valid. Go back and try again."] =
            "Begäran var inte giltig. Gå tillbaka och försök igen.",
        ["Sign in required"] = "Inloggning krävs",
        ["Your session has expired. Sign in again."] = "Din session har gått ut. Logga in igen.",
        ["Access denied"] = "Åtkomst nekad",
        ["Your account does not have permission for that page."] =
            "Ditt konto har inte behörighet till den sidan.",
        ["Page not found"] = "Sidan hittades inte",
        ["That page does not exist. It may have moved or been deleted."] =
            "Sidan finns inte. Den kan ha flyttats eller tagits bort.",
        ["Method not allowed"] = "Metoden tillåts inte",
        ["That action is not available from this page."] =
            "Åtgärden är inte tillgänglig från den här sidan.",
        ["Go to timesheet"] = "Gå till tidrapporten",
        ["Request ID"] = "Förfrågnings-ID",
        ["Your account does not have permission to open that page."] =
            "Ditt konto har inte behörighet att öppna den sidan.",
        ["Administrator pages are limited to administrator accounts. If you need access, ask an administrator to change your role."] =
            "Administratörssidor är begränsade till administratörskonton. Om du behöver åtkomst ber du en administratör ändra din roll.",
        ["Security settings"] = "Säkerhetsinställningar",

        // About
        ["A private, multiuser time report for Linux servers. Each account has its own encrypted database."] =
            "En privat tidrapport för flera användare på Linux-servrar. Varje konto har sin egen krypterade databas.",
        ["No telemetry"] = "Ingen telemetri",
        ["Security model"] = "Säkerhetsmodell",
        ["Built for a small team behind your own reverse proxy."] =
            "Byggd för ett litet team bakom din egen omvända proxy.",
        ["Passwords are hashed with PBKDF2-HMAC-SHA256 and a per-user salt."] =
            "Lösenord hashas med PBKDF2-HMAC-SHA256 och ett unikt salt per användare.",
        ["TOTP is mandatory for every account, with one-time recovery codes."] =
            "TOTP är obligatoriskt för varje konto, med engångskoder för återställning.",
        ["Every user database is encrypted with SQLCipher using a key derived from the deployment master key."] =
            "Varje användardatabas krypteras med SQLCipher och en nyckel som härleds från driftsättningens huvudnyckel.",
        ["Cookies are HttpOnly, SameSite=Strict and Secure in production."] =
            "Kakor är HttpOnly, SameSite=Strict och Secure i produktion.",
        ["Content Security Policy blocks inline scripts, inline styles and third-party requests."] =
            "Content Security Policy blockerar inline-skript, inline-stilar och tredjepartsförfrågningar.",
        ["The admin UI does not expose another user's timesheet. Audited credential resets are available for account recovery, so grant the administrator role carefully."] =
            "Admin-gränssnittet visar inte någon annan användares tidrapport. Loggade återställningar av inloggningsuppgifter finns för kontorecovery, så ge administratörsrollen varsamt.",
        ["Version information from the running assembly."] = "Versionsinformation från den körande sammansättningen.",
        ["License"] = "Licens",
        ["Source code"] = "Källkod",
        ["Deployment guide"] = "Driftguide",
        ["This web build is a private, multiuser deployment."] =
            "Den här webbversionen är en privat driftsättning för flera användare.",

        // Status, validation and server messages
        ["Too many sign-in attempts. Wait a few minutes and try again."] =
            "För många inloggningsförsök. Vänta några minuter och försök igen.",
        ["Invalid username or password."] = "Fel användarnamn eller lösenord.",
        ["Too many incorrect codes. Sign in again to start a new verification."] =
            "För många felaktiga koder. Logga in igen för att starta en ny verifiering.",
        ["The authenticator code is not valid. Check the code and your device clock."] =
            "Autentiseringskoden är inte giltig. Kontrollera koden och enhetens klocka.",
        ["That recovery code is not valid or has already been used."] =
            "Återställningskoden är inte giltig eller har redan använts.",
        ["Too many setup attempts. Restart the service to generate a new setup token."] =
            "För många installationsförsök. Starta om tjänsten för att skapa en ny installations-token.",
        ["The setup token is not valid."] = "Installations-token är inte giltig.",
        ["The passwords do not match."] = "Lösenorden matchar inte.",
        ["The new passwords do not match."] = "De nya lösenorden matchar inte.",
        ["The current password is not correct."] = "Det nuvarande lösenordet är fel.",
        ["Entry updated."] = "Posten uppdaterades.",
        ["Entry added."] = "Posten lades till.",
        ["Entry deleted."] = "Posten togs bort.",
        ["That entry no longer exists."] = "Posten finns inte längre.",
        ["The entry could not be deleted."] = "Posten kunde inte tas bort.",
        ["The entry could not be saved."] = "Posten kunde inte sparas.",
        ["Project created."] = "Projektet skapades.",
        ["Project renamed."] = "Projektet bytte namn.",
        ["Project archived."] = "Projektet arkiverades.",
        ["Project restored."] = "Projektet återställdes.",
        ["Project deleted."] = "Projektet togs bort.",
        ["Entry type created."] = "Posttypen skapades.",
        ["Entry type renamed."] = "Posttypen bytte namn.",
        ["Entry type archived."] = "Posttypen arkiverades.",
        ["Entry type restored."] = "Posttypen återställdes.",
        ["Entry type deleted."] = "Posttypen togs bort.",
        ["Choose a valid type category."] = "Välj en giltig typkategori.",
        ["User created."] = "Användaren skapades.",
        ["The user no longer exists."] = "Användaren finns inte längre.",
        ["Type the username exactly to confirm deletion."] =
            "Ange användarnamnet exakt för att bekräfta borttagningen.",
        ["MFA reset for {0}. They must enroll again after signing in."] =
            "MFA återställd för {0}. De måste aktivera igen efter inloggning.",
        ["{0} disabled."] = "{0} inaktiverades.",
        ["{0} enabled."] = "{0} aktiverades.",
        ["{0} is now an administrator."] = "{0} är nu administratör.",
        ["{0} is no longer an administrator."] = "{0} är inte längre administratör.",
        ["{0} and their private database were deleted."] =
            "{0} och deras privata databas togs bort.",
        ["{0} was deleted, but their data directory could not be removed."] =
            "{0} togs bort, men deras datamapp kunde inte tas bort.",
        ["The daily overtime threshold must be between 0 and 24 hours."] =
            "Den dagliga övertidsgränsen måste vara mellan 0 och 24 timmar.",
        ["The weekly overtime threshold must be between 0 and 168 hours."] =
            "Veckans övertidsgräns måste vara mellan 0 och 168 timmar.",
        ["Backup retention must be between 1 and 365."] =
            "Antalet säkerhetskopior måste vara mellan 1 och 365.",
        ["Settings saved."] = "Inställningarna sparades.",
        ["New recovery codes generated. Save them now; they are shown only once."] =
            "Nya återställningskoder skapades. Spara dem nu; de visas bara en gång.",
        ["Two-factor authentication is enabled. Save your recovery codes next."] =
            "Tvåfaktorsautentisering är aktiverad. Spara återställningskoderna härnäst.",
        ["Password changed."] = "Lösenordet ändrades.",
        ["The demo guest account has fixed credentials. Create another user to test credential changes."] =
            "Demo-gästens inloggningsuppgifter är fasta. Skapa en annan användare för att testa ändringar.",
        ["You cannot disable your own account."] = "Du kan inte inaktivera ditt eget konto.",
        ["You cannot remove your own administrator role."] = "Du kan inte ta bort din egen administratörsroll.",
        ["You cannot delete your own account."] = "Du kan inte ta bort ditt eget konto.",
        ["You cannot {0} the last active administrator."] =
            "Du kan inte {0} den sista aktiva administratören.",
        ["That username is already in use."] = "Användarnamnet används redan.",
        ["Users must be 3–64 characters, start with a letter or digit, and contain only letters, digits, dot, underscore or hyphen."] =
            "Användarnamn måste vara 3–64 tecken, börja med en bokstav eller siffra och endast innehålla bokstäver, siffror, punkt, understreck eller bindestreck.",
        ["Display names must be 80 characters or fewer."] = "Visningsnamn får vara högst 80 tecken.",
        ["Two-factor authentication is already enabled."] = "Tvåfaktorsautentisering är redan aktiverad.",
        ["The authenticator code is not valid. Check the clock and try again."] =
            "Autentiseringskoden är inte giltig. Kontrollera klockan och försök igen.",
        ["Start the authenticator setup again."] = "Börja om autentiseringskonfigurationen.",
        ["Choose a password that is different from the current one."] =
            "Välj ett lösenord som skiljer sig från det nuvarande.",
        ["Enter a password."] = "Ange ett lösenord.",
        ["The password must be at least 12 characters."] = "Lösenordet måste vara minst 12 tecken.",
        ["The password must be at most 1024 characters."] = "Lösenordet får vara högst 1024 tecken.",
        ["The password must not be the same as the username."] =
            "Lösenordet får inte vara samma som användarnamnet.",
        ["Choose a less common password."] = "Välj ett mindre vanligt lösenord.",
        ["Choose a password with more variety."] = "Välj ett lösenord med mer variation.",
        ["{0} account(s)"] = "{0} konto(n)",
        ["{0} shown"] = "{0} visas",
        ["you"] = "du",
        ["demo"] = "demo",
        ["Build"] = "Bygge",
        ["Commit"] = "Incheckning",
        ["Account security"] = "Kontosäkerhet",
        ["TOTP"] = "TOTP",
        ["Setup required"] = "Konfiguration krävs",
        ["QR code for TimeReport authenticator setup"] = "QR-kod för TimeReport-autentisering"
    };

    public static string T(string english) =>
        LocalizationService.CurrentLanguage == "sv" &&
        Swedish.TryGetValue(english, out var translated)
            ? translated
            : english;

    public static string T(string english, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, T(english), args);

    public static IReadOnlyDictionary<string, string> SwedishTranslations => Swedish;
}

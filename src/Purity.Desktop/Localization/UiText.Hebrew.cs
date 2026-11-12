namespace Purity.Desktop.Localization
{
	public sealed partial class UiText
	{
		/// <summary>Worded without addressing a man or a woman, as Hebrew commands otherwise must</summary>
		public static readonly UiText Hebrew = new()
		{
			Settings = "הגדרות",
			Refresh = "רענון",
			NewPeriod = "מחזור חדש",
			ClosePeriod = "סגירת המחזור",
			RemovePeriod = "מחיקת המחזור",
			FoldPeriod = "כיווץ המחזור",
			OpenPeriodFrom = date => $"פתיחת המחזור מ{date}",
			From = "תחילת המחזור",
			To = "סוף המחזור",
			FromDayOrNight = "תחילת המחזור: יום או לילה",
			ToDayOrNight = "סוף המחזור: יום או לילה",
			Day = "יום",
			Night = "לילה",
			AfterDawn = "עונת היום",
			AfterDusk = "עונת הלילה",
			Skip = "דילוג",
			Skipped = "דולג",
			SkipTip = "השמטת המחזור הזה מחישוב וסת ההפלגה בגלל סיבה חיצונית (ניתוח, התקנת התקן תוך־רחמי וכדומה)",
			Next = "הבא",
			NoPeriodsYet = "אין עדיין מחזורים",
			Press = "יש ללחוץ על",
			WhenOneBegins = "כשמחזור מתחיל",

			Mikveh = "מקווה",
			VesetHodesh = "וסת החודש",
			VesetAflaga = "וסת ההפלגה",
			OnaBeinonit = "עונה בינונית",

			Today = "היום",
			Tomorrow = "מחר",
			InDays = days => days == 2 ? "בעוד יומיים" : $"בעוד {days} ימים",
			TodayLine = date => $"היום, {date}",
			HebrewDateTip = (date, night) => $"{date}, {(night ? "לילה" : "יום")}",
			GregorianNightTip = (first, second) =>
			{
				var names = HebrewDates.Culture.DateTimeFormat;
				var from = $"{names.GetDayName(first.DayOfWeek)} ה־{first.Day}";
				var to = $"ל{names.GetDayName(second.DayOfWeek)} ה־{second.Day} {names.MonthGenitiveNames[second.Month - 1]}";
				return first.Month == second.Month ? $"הלילה שבין {from} {to}" : $"הלילה שבין {from} {names.MonthGenitiveNames[first.Month - 1]} {to}";
			},

			DataFilePathLabel = "קובץ נתונים:",
			PickFileTip = "שימוש בקובץ נתונים קיים, למשל זה שמשמש את המחשבים האחרים",
			AppearanceLabel = "מראה:",
			AppearanceName = "מראה",
			SameAsSystem = "כמו במערכת",
			Light = "בהיר",
			Dark = "כהה",
			LanguageLabel = "שפה:",
			LanguageName = "שפה",
			CalendarLabel = "לוח שנה:",
			CalendarName = "לוח שנה",
			GregorianAndHebrew = "לועזי ועברי",
			HebrewOnly = "עברי בלבד",
			HebrewInEnglishLabel = "כתיבת תאריכים עבריים באותיות לועזיות",
			RestoreEarlier = "שחזור גרסה קודמת...",
			RestoreEarlierTip = "החזרת ההיסטוריה למצבה לפני שינוי. הגרסה שמוחלפת נשמרת גם היא.",
			MoveFile = "העברת קובץ הנתונים לתיקייה אחרת...",
			MoveFileTip = "העתקת ההיסטוריה לתיקייה אחרת, למשל תיקייה שמסונכרנת עם Dropbox או OneDrive, ושימוש בה משם. הקובץ הנוכחי נשאר כפי שהוא.",
			CreateReport = "יצירת דוח אורכי מחזורים",
			RemindersOpen = "תזכורות...",
			RemindersTitle = "תזכורות",
			RemindersWhere = "היכן:",
			RemindersWhereName = "היכן לשמור את התזכורות",
			RemindersFile = "קובץ",
			RemindersOther = "אחר",
			RemindersAbout = "על מה:",
			RemindersAboutName = "על מה להזכיר",
			RemindersMikvehOnly = "מקווה בלבד",
			RemindersAllDates = "כל התאריכים",
			RemindersChosen = "בחירה",
			RemindersWording = "נוסח:",
			RemindersWordingName = "נוסח התזכורות",
			RemindersDiscreet = "דיסקרטי",
			RemindersDetailed = "מפורט",
			RemindersNext = "התזכורות הבאות",
			RemindersNone = "אין תזכורות קרובות.",
			RemindersOpenPeriod = "יש מחזור פתוח. התזכורות שלו יופיעו אחרי שייסגר.",
			RemindersMore = "עוד",
			RemindersMikvehTime = "התראה למקווה:",
			RemindersOtherTime = "התראות אחרות:",
			RemindersMoveOffShabbat = "העברת התראות משבת ליום שישי בשעה 08:00",
			RemindersCalendarName = "שם היומן:",
			RemindersNotice = "הקובץ הוא העתק חד־פעמי. אם התאריכים משתנים, צריך לשמור אותו שוב. ייבוא הקובץ שומר את התאריכים אצל שירות היומן שלך.",
			RemindersSave = "שמירת קובץ תזכורות...",
			RemindersFileTitle = "קובץ תזכורות",
			RemindersFileType = "קובץ יומן",
			RemindersSaved = "קובץ התזכורות נשמר. אפשר לפתוח אותו כדי להוסיף את התזכורות ליומן.",
			RemindersFileFailed = "לא ניתן היה לכתוב את קובץ התזכורות.",
			RemindersNotSaved = "לא ניתן היה לשמור את בחירות התזכורות, ולכן Purity ייפתח בפעם הבאה עם הבחירות הקודמות.\n\n",
			RemindersGoogleTip = "היומן שלך ב־Google",
			RemindersGoogleNotInBuild = "Google אינו מוגדר בגרסה הזו",
			RemindersGoogleIntro = "Purity יוצר יומן משלו בחשבון Google שלך. הוא יכול לראות את שמות היומנים האחרים, אבל לא לקרוא אותם או לשנות אותם. התאריכים האלה יישמרו אצל Google.",
			RemindersConnect = "חיבור חשבון Google...",
			RemindersConnecting = "ממתין לכניסה שלך בדפדפן...",
			RemindersConnected = "מחובר. Purity מעדכן את היומן הזה בכל פעם שהתאריכים משתנים.",
			RemindersSendTest = "שליחת בדיקה",
			RemindersTestSent = "תזכורת בדיקה תגיע בעוד כשתי דקות.",
			RemindersDisconnect = "הפסקה והסרת הכול...",
			RemindersDisconnectQuestion = "להסיר את היומן של Purity מחשבון Google שלך?",
			RemindersDisconnectBody = "פעולה זו מוחקת את היומן ואת כל התזכורות שבו, ו־Purity שוכח את הכניסה.",
			RemindersDisconnectConfirm = "הסרה",
			RemindersSignInDone = "הכניסה הושלמה. אפשר לסגור את הכרטיסייה ולחזור אל Purity.",
			RemindersNoConnection = "Purity לא הצליח להגיע אל Google. הוא ינסה שוב בפעם הבאה שהתאריכים ישתנו.",
			RemindersSignInNeeded = "צריך להיכנס שוב ל־Google כדי שהתזכורות יישארו מעודכנות.",
			RemindersSignInNotFinished = "הכניסה ל־Google לא הושלמה.",
			RemindersCalendarFailed = "Google לא קיבל את העדכון. Purity ינסה שוב בפעם הבאה שהתאריכים ישתנו.",
			RemindersNotRemembered = "אין במחשב הזה מקום בטוח לשמור בו את הכניסה, ולכן יהיה צריך להתחבר שוב בפעם הבאה.",
			RemindersICloudTip = "היומן שלך ב־iCloud",
			RemindersCalDavTip = "שרת יומן שעובד ב־CalDAV, כמו Fastmail או Nextcloud",
			RemindersICloudIntro = "Purity יוצר יומן נפרד בחשבון iCloud שלך. הכניסה היא בסיסמה ספציפית ליישום, ולא בסיסמה של חשבון ה־Apple עצמו. אפשר ליצור סיסמה כזו [בניהול חשבון Apple]. למידע נוסף, אפשר להיכנס [לדף הזה].",
			RemindersCalDavIntro = "Purity יוצר יומן נפרד בשרת היומן שלך. התאריכים האלה יישמרו בשרת הזה.",
			RemindersServer = "שרת:",
			RemindersAppleAccount = "חשבון Apple:",
			RemindersAppSpecificPassword = "סיסמה ספציפית ליישום:",
			RemindersUserName = "שם משתמש:",
			RemindersPassword = "סיסמה:",
			RemindersConnectICloud = "חיבור iCloud",
			RemindersConnectCalDav = "חיבור",
			RemindersConnectingServer = "מתחבר...",
			RemindersDisconnectICloudQuestion = "להסיר את היומן של Purity מחשבון iCloud שלך?",
			RemindersDisconnectICloudBody = "פעולה זו מוחקת את היומן ואת כל התזכורות שבו, ו־Purity שוכח את הסיסמה. הסיסמה הספציפית ליישום עצמה ממשיכה לעבוד עד שמבטלים אותה ב־account.apple.com.",
			RemindersDisconnectCalDavQuestion = "להסיר את היומן של Purity משרת היומן שלך?",
			RemindersDisconnectCalDavBody = "פעולה זו מוחקת את היומן ואת כל התזכורות שבו, ו־Purity שוכח את הסיסמה.",
			RemindersICloudNoConnection = "Purity לא הצליח להגיע אל iCloud. הוא ינסה שוב בפעם הבאה שהתאריכים ישתנו.",
			RemindersCalDavNoConnection = "Purity לא הצליח להגיע אל שרת היומן. הוא ינסה שוב בפעם הבאה שהתאריכים ישתנו.",
			RemindersICloudSignInNeeded = "iCloud כבר לא מקבל את הסיסמה הספציפית ליישום. צריך ליצור סיסמה חדשה ולהתחבר שוב כדי שהתזכורות יישארו מעודכנות.",
			RemindersCalDavSignInNeeded = "שרת היומן כבר לא מקבל את הסיסמה. צריך להתחבר שוב כדי שהתזכורות יישארו מעודכנות.",
			RemindersICloudPasswordRefused = "iCloud לא קיבל את חשבון ה־Apple או את הסיסמה. צריך סיסמה ספציפית ליישום, ולא את הסיסמה של חשבון ה־Apple עצמו.",
			RemindersCalDavPasswordRefused = "שרת היומן לא קיבל את שם המשתמש או את הסיסמה.",
			RemindersNoCalendarService = "Purity לא מצא יומנים בכתובת הזו.",
			RemindersNotSecure = "הכתובת צריכה להתחיל ב־https://, כדי שהסיסמה לא תישלח בגלוי.",
			RemindersICloudFailed = "iCloud לא קיבל את העדכון. Purity ינסה שוב בפעם הבאה שהתאריכים ישתנו.",
			RemindersCalDavFailed = "שרת היומן לא קיבל את העדכון. Purity ינסה שוב בפעם הבאה שהתאריכים ישתנו.",
			Ok = "אישור",
			Cancel = "ביטול",
			ReportTitle = "דוח אורכי מחזורים",
			ReportFailed = "לא ניתן היה לכתוב את הדוח.",
			DataFileTitle = "קובץ נתונים",
			MoveToTitle = "העברת קובץ הנתונים אל",
			DataFileType = "נתוני Purity",

			EarlierVersions = "גרסאות קודמות",
			RestoreEllipsis = "שחזור...",
			NoBackupsYet = "במחשב הזה אין עדיין גרסאות קודמות. גרסה נשמרת בכל פעם שההיסטוריה משתנה.",
			ChooseBackup = "בכל פעם שההיסטוריה השתנתה, המחשב הזה שמר את הגרסה שהוחלפה. יש לבחור את הגרסה לשחזור:",

			Yes = "כן",
			No = "לא",
			Close = "סגירה",
			KeepOpen = "להשאיר פתוח",
			DuplicateToday = "בהיסטוריה כבר יש מחזור שמתחיל היום, ושני מחזורים לא יכולים להתחיל באותו הזמן.\n\nכדי להוסיף מחזור נוסף, יש לשנות קודם את תאריך ההתחלה של אותו מחזור.",
			EndNotYet = end => $"תאריך הסיום של המחזור, {end}, עדיין לא הגיע.",
			ClosePeriodAnyway = "לסגור את המחזור בכל זאת?",
			OddDates = "התאריכים של המחזור הזה נראים חריגים:",
			OddKeepOpen = "אם זו טעות הקלדה, אפשר להשאיר את המחזור פתוח ולתקן את התאריך.",
			OddEndsFirst = end => $"תאריך הסיום, {end}, לפני תאריך ההתחלה.",
			OddLong = days => $"הוא נמשך {days} ימים.",
			OddInPrevious = end => $"הוא מתחיל לפני סיום המחזור הקודם, {end}.",
			OddInNext = begin => $"הוא מסתיים אחרי תחילת המחזור הבא, {begin}.",
			OddBeforeNewest = begin => $"הוא מתחיל לפני תחילת המחזור האחרון, {begin}.",
			ChangeClosedTitle = "לשנות מחזור שכבר נסגר?",
			ChangeClosedStart = "המחזור הזה כבר נסגר, ותאריך ההתחלה שלו ישתנה:",
			ChangeClosedEnd = "המחזור הזה כבר נסגר, ותאריך הסיום שלו ישתנה:",
			ChangeNow = date => $"עכשיו: {date}",
			ChangeNew = date => $"חדש: {date}",
			ChangeClosedRefresh = "התאריכים שלו יחושבו מחדש בלחיצה על רענון.",
			DateWithOnah = (date, night) => night ? $"{date}, בלילה" : $"{date}, ביום",
			Change = "שינוי",
			CloseAnyway = "לסגור בכל זאת?",
			RemoveQuestion = "למחוק את המחזור הזה?",

			InCopy = name => $"בעותק \"{name}\"",
			KeepCopy = "לשמור את מה שבעותק",
			InBackup = when => $"בגיבוי מתאריך {when}",
			KeepBackup = "לשמור את מה שבגיבוי",
			InDataFile = "בקובץ הנתונים",
			KeepFile = "לשמור את מה שבקובץ",
			KeepThisComputer = "לשמור את מה שבמחשב הזה",
			ConflictIntro = "ההיסטוריה שונתה בשני מקומות, ושתי הגרסאות אינן מסכימות כאן.\n\nבמחשב הזה:\n",
			WhichVersion = "איזו גרסה לשמור?",
			Nothing = "כלום",
			PeriodRange = (begin, end) => $"{begin} עד {end}",
			OpenSuffix = ", פתוח",
			SkippedSuffix = ", דולג",
			NightSuffix = " (לילה)",
			DaySuffix = " (יום)",

			WarnSelfTest = "בדיקת חישובי התאריכים של Purity נכשלה במחשב הזה, ולכן לא מוצגים תאריכים. אין להסתמך על העותק הזה של היישום.",
			WarnChooseFile = "יש לבחור קובץ נתונים בהגדרות.",
			WarnUnreadable = "לא ניתן לקרוא את קובץ הנתונים. שום דבר לא ייכתב אליו עד שאפשר יהיה לקרוא אותו; הרישומים נשמרים במחשב הזה.",
			WarnTwoVersions = "שתי גרסאות של ההיסטוריה אינן מסכימות. יש ללחוץ על רענון כדי לבחור איזו לשמור. עד אז שום דבר לא נשמר.",
			WarnNotWritten = "לא ניתן היה לכתוב לקובץ הנתונים. הרישומים נשמרים במחשב הזה, ויישמרו בקובץ בשינוי הבא.",
			WarnCopies = count => count == 1
				? "לצד קובץ הנתונים נמצא עותק שיישום הסנכרון השאיר, והוא עדיין לא צורף. יש ללחוץ על רענון כדי לצרף אותו."
				: $"לצד קובץ הנתונים נמצאים {count} עותקים שיישום הסנכרון השאיר, והם עדיין לא צורפו. יש ללחוץ על רענון כדי לצרף אותם.",
			WarnVault = "לא ניתן היה לכתוב או לקרוא את עותק הגיבוי במחשב הזה. השמירה לקובץ הנתונים עדיין פועלת.",

			Unreadable =
				"לא ניתן היה לקרוא את קובץ הנתונים, ולכן מוצג העותק שהמחשב הזה שמר בפעם האחרונה, אם יש כזה. ייתכן שרישומים אחרונים חסרים.\n\n" +
				"שום דבר לא ייכתב לקובץ הנתונים עד שאפשר יהיה לקרוא אותו. כל מה שיוזן עכשיו נשמר במחשב הזה ויתווסף כשאפשר יהיה לקרוא את הקובץ, למשל כשהסנכרון יסתיים.",
			SelfTestFailed =
				"Purity בודק את חישובי התאריכים בכל הפעלה, ובמחשב הזה התוצאות שונות מהתוצאות המאושרות. " +
				"לכן לא מוצגים תאריכים, וקובץ הנתונים לא נפתח, כך ששום דבר בו לא יכול להשתנות.\n\n" +
				"נא לא להסתמך על העותק הזה של היישום, ולדווח למפתח מה הבדיקה מצאה:\n\n",
			UnsavedOnClose = "חלק מהרישומים עדיין לא נשמרו בקובץ הנתונים.\n\nהם נשמרים במחשב הזה, ו־Purity ישמור אותם בפעם הבאה שייפתח.",

			BringThemIn = "לצרף",
			NotNow = "לא עכשיו",
			CopyUnreadable = name => $"לא ניתן לקרוא את \"{name}\", ולכן הוא נשאר במקומו.",
			CopyNotWritten = name => $"\"{name}\" נשאר במקומו, כי לא ניתן היה לכתוב לקובץ הנתונים.",
			CopyDisagrees = name => $"\"{name}\" אינו מסכים עם ההיסטוריה לגבי מחזור מסוים. יש ללחוץ על רענון כשמוכנים לבחור.",
			CopyLeft = name => $"\"{name}\" נשאר במקומו.",
			CopyBroughtIn = name => $"\"{name}\" צורף ועבר לגיבויים של המחשב הזה.",
			CopyBackupFailed = name => $"הרישומים של \"{name}\" נמצאים בהיסטוריה, אבל העותק נשאר לצד קובץ הנתונים, כי לא ניתן היה לכתוב לגיבויים של המחשב הזה.",
			CopyChanged = name => $"\"{name}\" השתנה בזמן הצירוף, ולכן הוא נשאר במקומו.",
			CopyNotRemoved = name => $"הרישומים של \"{name}\" נמצאים בהיסטוריה, אבל לא ניתן היה להסיר את העותק מלצד קובץ הנתונים.",
			AndMore = count => $"ועוד {count}",
			CopiesQuestion = (count, names) =>
				(count == 1 ? "יישום הסנכרון השאיר עותק" : $"יישום הסנכרון השאיר {count} עותקים") +
				" של קובץ הנתונים לצדו, מזמן ששני מכשירים שמרו בו־זמנית:\n\n" + names + "\n\n" +
				(count == 1 ? "ייתכן שיש בו רישומים שחסרים כאן." : "ייתכן שיש בהם רישומים שחסרים כאן.") +
				"\n\nשום דבר לא יימחק מההיסטוריה. אם עותק אינו מסכים לגבי מחזור מסוים, אפשר לבחור איזו גרסה לשמור. " +
				"לאחר מכן כל עותק יעבור לגיבויים של המחשב הזה.",
			BringInTitle = count => count == 1 ? "לצרף אותו?" : "לצרף אותם?",
			CopiesReport = (setAside, added) =>
			{
				var one = setAside == 1;
				var copies = one ? "צורף עותק אחד" : $"צורפו {setAside} עותקים";
				var periods = added switch
				{
					0 => one ? "לא היה בו שום דבר חדש" : "לא היה בהם שום דבר חדש",
					1 => "נוסף " + (one ? "ממנו" : "מהם") + " מחזור חדש אחד",
					_ => "נוספו " + (one ? "ממנו" : "מהם") + $" {added} מחזורים חדשים",
				};
				return $"{copies}. {periods}, ו{(one ? "הוא נמצא" : "הם נמצאים")} עכשיו בגיבויים של המחשב הזה.";
			},

			FirstStartTitle = "ברוכים הבאים ל־Purity",
			FirstStart =
				"Purity שומר את ההיסטוריה שלך בקובץ נתונים אחד. אפשר להתחיל היסטוריה חדשה במחשב הזה, או לפתוח את הקובץ שכבר יש לך, " +
				"למשל ב־Dropbox, ב־Google Drive או ב־OneDrive, כדי שכל המחשבים שלך ישתמשו בו. " +
				"היסטוריה חדשה אפשר להעביר מאוחר יותר לתיקייה מסונכרנת, בהגדרות.",
			StartNew = "התחלת היסטוריה חדשה",
			OpenMine = "פתיחת קובץ הנתונים שלי...",
			SettingsNotSaved = "לא ניתן היה לשמור את ההגדרות, ולכן Purity ישאל שוב על קובץ הנתונים בפעם הבאה.\n\n",

			WipeOut = "מחיקת כל ההיסטוריה...",
			WipeOutTip = "מחיקה לצמיתות של ההיסטוריה ושל כל הגיבויים שלה",
			WipeTitle = "למחוק את כל ההיסטוריה?",
			WipeBody =
				"Purity ימחק את ההיסטוריה ואת כל הגיבויים שלה.\n\n" +
				"אם קובץ הנתונים נמצא בתיקייה שמסונכרנת ב־Dropbox, ב־Google Drive, ב־OneDrive או ב־iCloud, הוא יימחק מכל המחשבים שמשתמשים בו. " +
				"במחשבים אחרים הגיבויים שלהם נשארים עד שימחקו גם שם, ושירותי הסנכרון שומרים קבצים שנמחקו לזמן מה, באשפה או בהיסטוריה שלהם.",
			WipeSure = "אי אפשר לבטל את זה.",
			WipeForGood = "מחיקה לצמיתות",
			WipedTitle = "ההיסטוריה נמחקה",
			Wiped = "Purity ייסגר עכשיו. כשיופעל שוב, הוא יתחיל מההתחלה.",
			WipeLeftTitle = "לא הכול נמחק",
			WipeLeft = items => $"Purity לא הצליח למחוק את אלה, ולכן כדאי למחוק אותם ידנית:\n\n{items}\n\nPurity ייסגר עכשיו.",
			WipeCalendarLeft = "היומן של Purity בחשבון Google שלך: אפשר למחוק אותו בהגדרות של Google Calendar",
			WipeICloudCalendarLeft = "היומן של Purity בחשבון iCloud שלך: אפשר למחוק אותו ביישום לוח השנה או ב־icloud.com",
			WipeCalDavCalendarLeft = "היומן של Purity בשרת היומן שלך: אפשר למחוק אותו ביישום היומן שלך",
			ClosePurity = "סגירת Purity",
			MoveOntoFile =
				"שם כבר קיים קובץ בשם הזה, ו־Purity לא יחליף אותו, כי ייתכן שיש בו היסטוריה של מישהו.\n\n" +
				"כדי להשתמש בקובץ הזה, יש לבחור אותו בכפתור ... שבהגדרות.",
			CantMoveYet = "עדיין לא ניתן להעביר את ההיסטוריה, כי חלק מהרישומים לא הגיעו לקובץ הנתונים. אפשר לנסות שוב כשיגיעו.",
			CopyFailed = "לא ניתן היה להעתיק את ההיסטוריה לשם, ולכן היא נשארת במקומה.\n\n",
			Moved = (to, from) => $"ההיסטוריה נשמרת עכשיו ב־{to}.\n\nהקובץ הישן עדיין נמצא ב־{from}, ללא שינוי, ו־Purity כבר לא משתמש בו.",

			RestoreConfirm = when => $"להחזיר את ההיסטוריה למצבה עד {when}?",
			RestoreConfirmBody =
				"ההיסטוריה כפי שהיא עכשיו תישמר גם היא כגיבוי, כך שאפשר יהיה לחזור אליה באותה דרך. " +
				"רישומים שעדיין לא הגיעו לקובץ הנתונים יישמרו.",
			Restore = "שחזור",
			BackupUnreadable = "לא ניתן לקרוא את הגיבוי הזה, ולכן שום דבר לא השתנה.",
			RestoreNotWritten = "לא ניתן היה לכתוב לקובץ הנתונים, ולכן הגרסה המשוחזרת נשמרת במחשב הזה ותישמר בקובץ בשינוי הבא.",
			Restored = when => $"ההיסטוריה חזרה למצבה עד {when}. הגרסה שהייתה לפני רגע נשמרה גם היא כגיבוי.",
			Until = when => $"עד {when}: ",
			NoPeriods = "אין מחזורים",
			PeriodCount = count => count == 1 ? "מחזור אחד" : $"{count} מחזורים",
			LastBeginning = date => $", האחרון התחיל ב־{date}",
			SameAsNow = ", זהה למצב הנוכחי",
			CantBeRead = "לא ניתן לקריאה",

			AppearanceNotSaved = "לא ניתן היה לשמור את המראה, ולכן Purity ייפתח בפעם הבאה עם המראה הקודם.\n\n",
			LanguageNotSaved = "לא ניתן היה לשמור את השפה, ולכן Purity ייפתח בפעם הבאה בשפה הקודמת.\n\n",
			CalendarNotSaved = "לא ניתן היה לשמור את לוח השנה, ולכן Purity ייפתח בפעם הבאה עם הלוח הקודם.\n\n",
		};
	}
}

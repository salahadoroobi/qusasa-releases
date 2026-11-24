# تعليمات البناء والنشر | Build & Release Instructions

## محليًا (يتطلب Windows)

### المتطلبات الأساسية
1. **Visual Studio 2022** مع workload **\".NET Framework 4.8 targeting pack\"**
2. **Inno Setup 6** (ISCC.exe) - لتوليد المثبت
3. **Git** - للتحكم بالإصدارات

### البناء
`powershell
# بناء التطبيق
dotnet build Qusasa.csproj -c Release --nologo -p:UpdateFeedUrl=\"https://github.com/salahadoroobi/qusasa-releases/releases/latest/download/stable.json\" -p:OcrRelayUrl=\"https://qusasa-ocr-relay.chirpydate4.chatgpt.site/v1/scan\"

# بناء المثبت (Inno Setup)
& \"C:\\Program Files (x86)\\Inno Setup 6\\ISCC.exe\" /Q Qusasa.iss

# النتيجة: bin/Installer/Qusasa-1.29-Engine.exe
`

### النشر على GitHub
`powershell
# 1. إنشاء المستودع على GitHub (salahadoroobi/qusasa أو salahadoroobi/Qusasa)

# 2. ربط المستودع البعيد
git remote add origin https://github.com/salahadoroobi/qusasa.git

# 3. الدفع
git push -u origin master

# 4. إنشاء وسم للإصدار (هذا سيطلق GitHub Actions)
git tag v1.29.0
git push origin v1.29.0
`

## عبر GitHub Actions (موصى به)

تم إضافة workflow في .github/workflows/build.yml الذي:
- يبني التطبيق على Windows
- يثبت .NET Framework 4.8 و Inno Setup
- يولد المثبت (Qusasa-1.29-Engine.exe)
- يرفعه كـ artifact
- عند وسم (tag) مثل 1.29.0، ينشئ GitHub Release تلقائياً

### خطوات النشر:
1. أنشئ المستودع على GitHub
2. ادفع الكود: git push -u origin master
3. أنشئ وسم: git tag v1.29.0 && git push origin v1.29.0
4. سيتولى GitHub Actions الباقي

## ملاحظات
- UpdateFeedUrl يشير لمستودع الإصدارات qusasa-releases
- OcrRelayUrl يشير لخدمة OCR السحابية
- المفتاح العمومي للتوقيع في UpdateTrust.xml
- المفتاح الخاص مخزن محليًا ومحمي بـ DPAPI

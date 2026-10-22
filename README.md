# Simplex Calculator for Android

Aplikasi Android .NET MAUI untuk optimasi program linear. Solver C# menggunakan algoritma yang sama dengan project WPF Simplex Calculator.

## Fitur

- Maksimasi dan minimasi
- Kendala `<=`, `>=`, dan `=`
- Variabel keputusan diasumsikan nonnegatif (`x1, x2, ... >= 0`)
- Metode two-phase dan tableau setiap iterasi
- Deteksi model infeasible dan unbounded

Koefisien fungsi tujuan dipisahkan koma. Masukkan satu kendala per baris, misalnya:

```text
1, 0 <= 4
0, 2 <= 12
3, 2 <= 18
```

Angka desimal menggunakan titik.

## Build APK di GitHub Actions

Project ini sudah menyertakan workflow di `.github/workflows/build-android.yml`. Unggah seluruh isi folder project ini ke repository GitHub, termasuk folder `.github`, lalu buka tab **Actions** dan jalankan workflow **Build Android APK**. Workflow juga berjalan otomatis setiap kali ada push ke branch `main` atau `master`.

Setelah workflow berhasil:

1. Buka run terbaru pada tab **Actions**.
2. Di bagian **Artifacts**, unduh `simplex-calculator-debug-apk`.
3. Ekstrak ZIP artifact dan instal APK di perangkat Android.

APK debug ditandatangani untuk pengujian. Untuk distribusi publik atau Google Play, buat konfigurasi signing release dengan keystore yang disimpan sebagai GitHub Secrets; jangan commit keystore atau password ke repository.

## Build lokal

Memerlukan .NET 8 SDK dan workload `maui-android`:

```powershell
dotnet workload install maui-android
dotnet build SimplexCalc.Mobile.csproj -f net8.0-android -c Debug -p:AndroidPackageFormat=apk
```

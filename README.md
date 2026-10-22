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

## Build lokal

Memerlukan .NET 8 SDK dan workload `maui-android`:

```powershell
dotnet workload install maui-android
dotnet build SimplexCalc.Mobile.csproj -f net8.0-android -c Debug -p:AndroidPackageFormat=apk
```

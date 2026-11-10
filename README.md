# NusantaraCrown AR

Aplikasi augmented reality berbasis Unity 6 URP untuk mencoba 10 mahkota adat tradisional Indonesia langsung di kepala lewat kamera, dilengkapi kartu informasi filosofi budaya.

![NusantaraCrown AR Banner](Assets/UI/UI_Header_HUD.png)

## Fitur

- Virtual head fitting: Mahkota mengikuti posisi dan rotasi kepala pengguna secara real-time.
- Dual tracking: Memakai AR Foundation (ARCore dan ARKit) untuk smartphone, dengan fallback deteksi wajah neural (Unity Sentis) jika perangkat belum mendukung Google ARCore.
- 10 mahkota tradisional: Model 3D detail material PBR untuk pria dan wanita dari 5 pulau besar (Sumatera, Jawa, Kalimantan, Sulawesi, dan Papua).
- Antarmuka: Filter gender pria dan wanita, carousel pulau, kartu filosofi budaya tiga poin (makna, bahan, momen penggunaan), serta tangkapan layar kamera.

## Daftar mahkota adat

| Pulau | Pria | Wanita | Filosofi |
| :--- | :--- | :--- | :--- |
| Sumatera | Deta Saluak | Suntiang Gadang | Kehormatan penghulu Minangkabau dan mahkota bertingkat simbol tanggung jawab rumah tangga. |
| Jawa | Kuluk Kanigaran | Cunduk Mentul | Beludru hitam berhias sulaman emas keraton dan kembang goyang pengantin. |
| Kalimantan | Lavung Baling Dayak | Lavung Manik Dayak | Hiasan bulu enggang lambang kesatria dan tenun manik-manik khas Dayak Kenyah. |
| Sulawesi | Passapu Patonro | Saloko Poci | Lilitan kain merah destar Bugis Makassar dan mahkota lengkung emas bangsawan perempuan. |
| Papua | Mahkota Kepala Suku | Mahkota Adat Wanita | Mahkota bulu kasuari atau cenderawasih dan jalinan kulit kayu berhias kerang cowrie. |

## Teknologi

- Engine: Unity 6 (6000.6.8f1)
- Render pipeline: Universal Render Pipeline (URP 17.6)
- XR stack: AR Foundation 6.6.2 (ARCore + ARKit)
- AI inference: Unity Sentis 2.6.1
- Target platform: Android (ARM64, Android 8.0+) dan iOS (iOS 14.0+)

## Build dan rilis otomatis

Workflow GitHub Actions di `.github/workflows/build-and-release.yml` mengompilasi installer secara otomatis:
- Android: Menghasilkan file instalasi `.apk` (ARM64).
- iOS: Menghasilkan proyek Xcode dan mengemas file installer `.ipa`.
- Rilis: Otomatis menerbitkan installer `.apk` dan `.ipa` ke halaman GitHub Releases.

## Lisensi

Hak Cipta © 2026 NusantaraCrown AR Team.

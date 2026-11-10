# NusantaraCrown AR (Nusantara-AR) 👑🇮🇩

Aplikasi *Augmented Reality (AR)* interaktif berbasis **Unity 6 URP** untuk mencoba 10 mahkota adat tradisional Indonesia secara langsung di atas kepala pengguna (*virtual face-hat tracking*), lengkap dengan kartu edukasi filosofi budaya Nusantara.

![NusantaraCrown AR Banner](Assets/UI/UI_Header_HUD.png)

---

## 🌟 Fitur Utama

1. **Virtual Head Fitting (3D Face & Hat Tracking)**:
   - Mahkota terpasang presisi di atas kepala mengikuti posisi, kemiringan (*pitch*), tolehan (*yaw*), dan rotasi (*roll*) kepala pengguna secara real-time.
   - **Dual-Mode Tracking Engine**:
     - **Mode Native AR**: Menggunakan **AR Foundation (ARCore / ARKit)** untuk perangkat mobile.
     - **Mode AI Fallback / Webcam**: Menggunakan deteksi wajah neural (*Unity Sentis / Inference Engine*) sehingga dapat berjalan di laptop/PC atau smartphone yang belum mendukung Google Play Services for AR.
2. **Koleksi 10 Mahkota Tradisional 5 Pulau Besar**:
   - Model 3D detail PBR (*Physically-Based Rendering*) dengan material emas, songket tenun, bulu enggang gading, manik kaca, dan kulit kayu alami.
   - Tampilan proporsional untuk Pria (*Cowok*) dan Wanita (*Cewek*).
3. **Antarmuka Modern (Glassmorphism UI/UX)**:
   - Terintegrasi langsung dengan desain Figma.
   - Pilihan toggle gender **Wanita** dan **Pria**.
   - Carousel kartu pulau interaktif (Sumatera, Jawa, Kalimantan, Sulawesi, Papua).
   - Modal informasi filosofi budaya 3 poin (Makna Simbolik, Bahan Tradisional, Momen Penggunaan).
   - Fitur capture kamera & preview hasil foto.

---

## 🏛️ Daftar Mahkota Tradisional

| Pulau | Pria | Wanita | Filosofi Singkat |
| :--- | :--- | :--- | :--- |
| **Sumatera** | **Deta Saluak** | **Suntiang Gadang** | Lambang kehormatan penghulu Minangkabau & mahkota bertingkat emas lambang tanggung jawab ibu rumah tangga. |
| **Jawa** | **Kuluk Kanigaran** | **Cunduk Mentul** | Beludru hitam berhias sulaman emas keraton Yogyakarta/Solo & kembang goyang cunduk mentul pengantin. |
| **Kalimantan** | **Lavung Baling Dayak** | **Lavung Manik Dayak** | Hiasan kepala bulu burung Enggang lambang kesatria & tenun manik-manik khas suku Dayak Kenyah. |
| **Sulawesi** | **Passapu Patonro** | **Saloko Poci** | Lilitan kain merah destar Bugis Makassar & mahkota lengkung emas bangsawan perempuan. |
| **Papua** | **Mahkota Kepala Suku** | **Mahkota Adat Wanita** | Mahkota bulu kasuari dan cenderawasih kepala suku & jalinan kulit kayu berhias kerang cowrie laut. |

---

## 🛠️ Spesifikasi Teknologi

- **Game Engine**: Unity 6 (6000.0.8f1)
- **Render Pipeline**: Universal Render Pipeline (URP 17.6)
- **XR Stack**: AR Foundation 6.6.2 (ARCore + ARKit)
- **AI Inference**: Unity Sentis / AI Inference 2.6.1
- **UI Framework**: Unity uGUI + TextMeshPro
- **Target Platform**: Android (ARM64, Android 8.0+) & iOS (iOS 14.0+)

---

## 🚀 Alur CI/CD (GitHub Actions)

Repository ini dilengkapi dengan alur otomatisasi **GitHub Actions** (`.github/workflows/build-and-release.yml`):
- **Android Build**: Mengompilasi proyek Unity ke format installer `.apk` (ARM64).
- **iOS Build**: Menghasilkan proyek Xcode & paket `.ipa` untuk iOS.
- **Automated Release**: Setiap kali tag versi (seperti `v0.0.1`) dibuat, file `.apk` dan `.ipa` akan otomatis diunggah ke menu **Releases** di GitHub.

---

## 📄 Lisensi
Hak Cipta © 2026 NusantaraCrown AR Team. Dibuat untuk pelestarian warisan budaya Indonesia melalui teknologi imersif.

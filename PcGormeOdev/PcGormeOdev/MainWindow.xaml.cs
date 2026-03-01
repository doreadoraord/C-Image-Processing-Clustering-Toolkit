using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Threading.Tasks;
using System.Linq;

namespace PcGormeOdev
{
    // ==================================================================================
    // BÖLÜM 1: VERİ MODELLERİ
    // ==================================================================================

    public class KumeSonucu
    {
        public int Id { get; set; }
        public int PikselSayisi { get; set; }
        public string Kirmizi { get; set; }
        public string Yesil { get; set; }
        public string Mavi { get; set; }
        public byte GercekKirmizi { get; set; }
        public byte GercekYesil { get; set; }
        public byte GercekMavi { get; set; }
        public double Yogunluk { get; set; }
    }

    public partial class MainWindow : Window
    {
        private BitmapImage _orijinalResimBitmap;
        private byte[] _orijinalPikseller;
        private byte[] _islenmisPikseller;

        private int _genislik;
        private int _yukseklik;
        private int _satirBaytSayisi;

        private Point _sonFarePozisyonu;
        private bool _surukleniyorMu = false;

        private struct RenkVektoru { public double R, G, B; }
        private struct Matris3x3 { public double M00, M01, M02, M10, M11, M12, M20, M21, M22; }

        public MainWindow()
        {
            InitializeComponent();

            // YENİ ALGORİTMA LİSTEYE EKLENDİ
            OperationComboBox.ItemsSource = new List<string> {
                "Seçiniz...", "Gri Format (Normal)", "Gri Format (U Formatı)", "Histogram",
                "K-Means (Öklid, Intensity)", "K-Means (Öklid, ND)",
                "K-Means (Mahalanobis, Intensity)", "K-Means (Mahalanobis, ND)",
                "Expectation Maximization (GMM)", "Kenar Bulma"
            };
        }

        // ==================================================================================
        // BÖLÜM 2: XAML UYUMLULUĞU VE FARE OLAYLARI (ZOOMSUZ, PİKSEL ODAKLI)
        // ==================================================================================

        private void ImgSol_MouseMove(object sender, MouseEventArgs e) { FareBilgisiGuncelle(sender, e, false); }
        private void ImgSol_MouseDown(object sender, MouseButtonEventArgs e) { FareBilgisiGuncelle(sender, e, true); }
        private void ImgSol_MouseUp(object sender, MouseButtonEventArgs e) { }
        private void ImgSol_MouseWheel(object sender, MouseWheelEventArgs e) { }

        private void ImgSag_MouseMove(object sender, MouseEventArgs e) { FareBilgisiGuncelle(sender, e, false); }
        private void ImgSag_MouseDown(object sender, MouseButtonEventArgs e) { FareBilgisiGuncelle(sender, e, true); }
        private void ImgSag_MouseUp(object sender, MouseButtonEventArgs e) { }
        private void ImgSag_MouseWheel(object sender, MouseWheelEventArgs e) { }

        private void Image_MouseMove(object sender, MouseEventArgs e) { FareBilgisiGuncelle(sender, e, false); }
        private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { FareBilgisiGuncelle(sender, e, true); }
        private void Image_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { }

        private void Hist_MouseWheel(object sender, MouseWheelEventArgs e) { }
        private void Hist_MouseDown(object sender, MouseButtonEventArgs e) { }
        private void Hist_MouseUp(object sender, MouseButtonEventArgs e) { }
        private void Hist_MouseMove(object sender, MouseEventArgs e) { }


        // ==================================================================================
        // BÖLÜM 3: DOSYA YÜKLEME VE İŞLEM TETİKLEME
        // ==================================================================================

        private void LoadImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dosyaAc = new Microsoft.Win32.OpenFileDialog { Filter = "Resimler|*.jpg;*.png;*.bmp" };
            if (dosyaAc.ShowDialog() == true)
            {
                _orijinalResimBitmap = new BitmapImage(new Uri(dosyaAc.FileName));
                OriginalImage.Source = _orijinalResimBitmap;

                FormatConvertedBitmap fcb = new FormatConvertedBitmap(_orijinalResimBitmap, PixelFormats.Bgra32, null, 0);
                _genislik = fcb.PixelWidth;
                _yukseklik = fcb.PixelHeight;
                _satirBaytSayisi = _genislik * 4;

                _orijinalPikseller = new byte[_yukseklik * _satirBaytSayisi];
                fcb.CopyPixels(_orijinalPikseller, _satirBaytSayisi, 0);

                TotalPixelText.Text = (_genislik * _yukseklik).ToString();
                ProcessedImage.Source = null;
                _islenmisPikseller = null;
                IstatistikleriTemizle();
            }
        }

        private void IstatistikleriTemizle()
        {
            StartValuesList.Items.Clear(); ResultListView.ItemsSource = null; IterationText.Text = "0";
            HistogramCanvas.Children.Clear();
        }

        private async void ProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (_orijinalPikseller == null) return;
            string islemAdi = OperationComboBox.SelectedItem as string;
            var kElemani = KValueComboBox.SelectedItem as ComboBoxItem;
            int k = (kElemani != null) ? int.Parse(kElemani.Content.ToString()) : 2;

            IstatistikleriTemizle();
            ProcessButton.IsEnabled = false; ProcessButton.Content = "İşleniyor...";

            await Task.Run(() =>
            {
                try
                {
                    switch (islemAdi)
                    {
                        case "Gri Format (Normal)": GriyeCevir(false); break;
                        case "Gri Format (U Formatı)": GriyeCevir(true); break;
                        case "Histogram": SadeceHistogramGoster(); break;
                        case "K-Means (Öklid, Intensity)": KMeans_Oklid_Yogunluk(k); break;
                        case "K-Means (Öklid, ND)": KMeans_Oklid_ND(k); break;
                        case "K-Means (Mahalanobis, Intensity)": KMeans_Mahalanobis_Yogunluk(k); break;
                        case "K-Means (Mahalanobis, ND)": KMeans_Mahalanobis_ND(k); break;
                        case "Expectation Maximization (GMM)": ExpectationMaximization_GMM_ND(k); break; // YENİ EKLENDİ
                        case "Kenar Bulma": KenarBulma(); break;
                    }
                }
                catch (Exception hata) { Application.Current.Dispatcher.Invoke(() => MessageBox.Show("Hata: " + hata.Message)); }
            });

            ProcessButton.IsEnabled = true; ProcessButton.Content = "Uygula";
        }

        // ==================================================================================
        // BÖLÜM 4: GÖRÜNTÜ İŞLEME ALGORİTMALARI
        // ==================================================================================

        private void GriyeCevir(bool uFormatiMi)
        {
            byte[] sonucPikseller = new byte[_orijinalPikseller.Length];
            byte[] griDegerler = new byte[_genislik * _yukseklik];

            for (int i = 0, j = 0; i < _orijinalPikseller.Length; i += 4, j++)
            {
                byte M = _orijinalPikseller[i], Y = _orijinalPikseller[i + 1], K = _orijinalPikseller[i + 2];
                byte gri = uFormatiMi ? (byte)(0.299 * K + 0.587 * Y + 0.114 * M) : (byte)((K + Y + M) / 3);
                sonucPikseller[i] = sonucPikseller[i + 1] = sonucPikseller[i + 2] = gri; sonucPikseller[i + 3] = 255;
                griDegerler[j] = gri;
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                ArayuzuGuncelle(sonucPikseller);
                int[] histogram = new int[256]; foreach (byte b in griDegerler) histogram[b]++;
                HistogramCiz(histogram, null);
            });
        }
        private void SadeceHistogramGoster() { GriyeCevir(true); }

        private void KenarBulma()
        {
            byte[] griResim = new byte[_genislik * _yukseklik];
            for (int i = 0, j = 0; i < _orijinalPikseller.Length; i += 4, j++)
                griResim[j] = (byte)(0.299 * _orijinalPikseller[i + 2] + 0.587 * _orijinalPikseller[i + 1] + 0.114 * _orijinalPikseller[i]);

            byte[] sonucPikseller = new byte[_orijinalPikseller.Length];
            int[,] maskeX = { { -1, 0, 1 }, { -2, 0, 2 }, { -1, 0, 1 } };
            int[,] maskeY = { { -1, -2, -1 }, { 0, 0, 0 }, { 1, 2, 1 } };

            for (int y = 1; y < _yukseklik - 1; y++)
            {
                for (int x = 1; x < _genislik - 1; x++)
                {
                    double sumX = 0, sumY = 0;
                    for (int ky = -1; ky <= 1; ky++)
                    {
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            int p = (y + ky) * _genislik + (x + kx);
                            sumX += griResim[p] * maskeX[ky + 1, kx + 1];
                            sumY += griResim[p] * maskeY[ky + 1, kx + 1];
                        }
                    }
                    byte val = (byte)Math.Min(255, Math.Abs(sumX) + Math.Abs(sumY));
                    int idx = (y * _genislik + x) * 4;
                    sonucPikseller[idx] = sonucPikseller[idx + 1] = sonucPikseller[idx + 2] = val; sonucPikseller[idx + 3] = 255;
                }
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                ArayuzuGuncelle(sonucPikseller);
                int[] h = new int[256]; for (int i = 0; i < sonucPikseller.Length; i += 4) h[sonucPikseller[i + 2]]++;
                HistogramCiz(h, null);
            });
        }

        private void KMeans_Oklid_Yogunluk(int k)
        {
            byte[] griResim = new byte[_genislik * _yukseklik];
            for (int i = 0, j = 0; i < _orijinalPikseller.Length; i += 4, j++)
                griResim[j] = (byte)(0.299 * _orijinalPikseller[i + 2] + 0.587 * _orijinalPikseller[i + 1] + 0.114 * _orijinalPikseller[i]);

            Random rasgele = new Random(Guid.NewGuid().GetHashCode());
            double[] merkezlerT = new double[k];
            int atanan = 0;
            while (atanan < k)
            {
                double aday = rasgele.Next(1, 256);
                bool varMi = false;
                for (int m = 0; m < atanan; m++) { if (merkezlerT[m] == aday) { varMi = true; break; } }
                if (!varMi) { merkezlerT[atanan] = aday; atanan++; }
            }
            BaslangicDegerleriniRaporla(merkezlerT, null);

            int iter = 0; int[] etiketler = new int[griResim.Length]; bool degisimVar = true;
            while (degisimVar)
            {
                degisimVar = false; iter++;
                long[] top = new long[k]; int[] say = new int[k];
                for (int i = 0; i < griResim.Length; i++)
                {
                    double minD = double.MaxValue; int enYakin = 0;
                    for (int t = 0; t < k; t++)
                    {
                        double d = Math.Abs(griResim[i] - merkezlerT[t]);
                        if (d < minD) { minD = d; enYakin = t; }
                    }
                    if (etiketler[i] != enYakin) { etiketler[i] = enYakin; degisimVar = true; }
                    top[enYakin] += griResim[i]; say[enYakin]++;
                }
                for (int i = 0; i < k; i++)
                {
                    if (say[i] > 0) merkezlerT[i] = (double)top[i] / say[i];
                    else merkezlerT[i] = rasgele.Next(1, 256);
                }
            }
            SonuclariIsleVeGoster(etiketler, merkezlerT, null, iter, griResim);
        }

        private void KMeans_Oklid_ND(int k)
        {
            Random rasgele = new Random(Guid.NewGuid().GetHashCode());
            RenkVektoru[] merkezlerT = new RenkVektoru[k];
            int atanan = 0;
            while (atanan < k)
            {
                int rsg = rasgele.Next(0, _genislik * _yukseklik) * 4;
                RenkVektoru aday = new RenkVektoru { B = _orijinalPikseller[rsg], G = _orijinalPikseller[rsg + 1], R = _orijinalPikseller[rsg + 2] };
                bool varMi = false;
                for (int m = 0; m < atanan; m++)
                {
                    if (merkezlerT[m].R == aday.R && merkezlerT[m].G == aday.G && merkezlerT[m].B == aday.B) { varMi = true; break; }
                }
                if (!varMi) { merkezlerT[atanan] = aday; atanan++; }
            }
            BaslangicDegerleriniRaporla(null, merkezlerT);

            int iter = 0; int[] etiketler = new int[_genislik * _yukseklik]; bool degisimVar = true;
            while (degisimVar)
            {
                degisimVar = false; iter++;
                long[] tR = new long[k], tG = new long[k], tB = new long[k]; int[] say = new int[k];
                for (int i = 0, p = 0; i < _orijinalPikseller.Length; i += 4, p++)
                {
                    double minD = double.MaxValue; int enYakin = 0;
                    for (int t = 0; t < k; t++)
                    {
                        double dr = _orijinalPikseller[i + 2] - merkezlerT[t].R;
                        double dg = _orijinalPikseller[i + 1] - merkezlerT[t].G;
                        double db = _orijinalPikseller[i] - merkezlerT[t].B;
                        double d = dr * dr + dg * dg + db * db;
                        if (d < minD) { minD = d; enYakin = t; }
                    }
                    if (etiketler[p] != enYakin) { etiketler[p] = enYakin; degisimVar = true; }
                    tB[enYakin] += _orijinalPikseller[i]; tG[enYakin] += _orijinalPikseller[i + 1]; tR[enYakin] += _orijinalPikseller[i + 2]; say[enYakin]++;
                }
                for (int i = 0; i < k; i++)
                {
                    if (say[i] > 0) { merkezlerT[i].R = (double)tR[i] / say[i]; merkezlerT[i].G = (double)tG[i] / say[i]; merkezlerT[i].B = (double)tB[i] / say[i]; }
                    else
                    {
                        int rsg = rasgele.Next(0, _genislik * _yukseklik) * 4;
                        merkezlerT[i] = new RenkVektoru { B = _orijinalPikseller[rsg], G = _orijinalPikseller[rsg + 1], R = _orijinalPikseller[rsg + 2] };
                    }
                }
            }
            SonuclariIsleVeGoster(etiketler, null, merkezlerT, iter, null);
        }

        private void KMeans_Mahalanobis_Yogunluk(int k)
        {
            byte[] griResim = new byte[_genislik * _yukseklik];
            for (int i = 0, j = 0; i < _orijinalPikseller.Length; i += 4, j++)
                griResim[j] = (byte)(0.299 * _orijinalPikseller[i + 2] + 0.587 * _orijinalPikseller[i + 1] + 0.114 * _orijinalPikseller[i]);

            Random rasgele = new Random(Guid.NewGuid().GetHashCode());
            double[] merkezlerT = new double[k]; double[] varyanslarV = new double[k];
            int atanan = 0;
            while (atanan < k)
            {
                double aday = rasgele.Next(1, 256);
                bool varMi = false;
                for (int m = 0; m < atanan; m++) { if (merkezlerT[m] == aday) varMi = true; }
                if (!varMi) { merkezlerT[atanan] = aday; varyanslarV[atanan] = 100.0; atanan++; }
            }
            BaslangicDegerleriniRaporla(merkezlerT, null);

            int iter = 0; int[] etiketler = new int[griResim.Length]; bool degisimVar = true;
            while (degisimVar)
            {
                degisimVar = false; iter++;
                long[] top = new long[k]; double[] topKare = new double[k]; int[] say = new int[k];
                for (int i = 0; i < griResim.Length; i++)
                {
                    double minD = double.MaxValue; int enYakin = 0;
                    for (int t = 0; t < k; t++)
                    {
                        double f = griResim[i] - merkezlerT[t];
                        double v = (varyanslarV[t] < 1.0) ? 1.0 : varyanslarV[t];
                        double d = (f * f) / v;
                        if (d < minD) { minD = d; enYakin = t; }
                    }
                    if (etiketler[i] != enYakin) { etiketler[i] = enYakin; degisimVar = true; }
                    top[enYakin] += griResim[i]; topKare[enYakin] += (griResim[i] * griResim[i]); say[enYakin]++;
                }
                for (int i = 0; i < k; i++)
                {
                    if (say[i] > 1)
                    {
                        merkezlerT[i] = (double)top[i] / say[i];
                        varyanslarV[i] = (topKare[i] / say[i]) - (merkezlerT[i] * merkezlerT[i]);
                    }
                    else { merkezlerT[i] = rasgele.Next(1, 256); varyanslarV[i] = 100.0; }
                    if (varyanslarV[i] < 1.0) varyanslarV[i] = 1.0;
                }
            }
            SonuclariIsleVeGoster(etiketler, merkezlerT, null, iter, griResim);
        }

        private void KMeans_Mahalanobis_ND(int kumeSayisi)
        {
            int toplamPiksel = _genislik * _yukseklik;
            int[] etiketler = new int[toplamPiksel];

            RenkVektoru[] merkezler = new RenkVektoru[kumeSayisi];
            Matris3x3[] tersKovaryanslar = new Matris3x3[kumeSayisi];
            double[] determinantCezalari = new double[kumeSayisi];

            Random rastgele = new Random(Guid.NewGuid().GetHashCode());

            int atanan = 0;
            while (atanan < kumeSayisi)
            {
                int rsgIndeks = rastgele.Next(0, toplamPiksel) * 4;
                RenkVektoru aday = new RenkVektoru { B = _orijinalPikseller[rsgIndeks], G = _orijinalPikseller[rsgIndeks + 1], R = _orijinalPikseller[rsgIndeks + 2] };

                bool cokYakin = false;
                for (int i = 0; i < atanan; i++)
                {
                    double d = Math.Abs(merkezler[i].R - aday.R) + Math.Abs(merkezler[i].G - aday.G) + Math.Abs(merkezler[i].B - aday.B);
                    if (d < 30) { cokYakin = true; break; }
                }
                if (!cokYakin || atanan == 0)
                {
                    merkezler[atanan] = aday;
                    tersKovaryanslar[atanan] = new Matris3x3 { M00 = 1, M11 = 1, M22 = 1 };
                    determinantCezalari[atanan] = 0;
                    atanan++;
                }
            }
            BaslangicDegerleriniRaporla(null, merkezler);

            bool degisimVar = true;
            int iterasyon = 0;

            while (degisimVar)
            {
                degisimVar = false; iterasyon++;
                double[] topR = new double[kumeSayisi], topG = new double[kumeSayisi], topB = new double[kumeSayisi];
                int[] sayac = new int[kumeSayisi];

                for (int i = 0, p = 0; i < _orijinalPikseller.Length; i += 4, p++)
                {
                    RenkVektoru piksel = new RenkVektoru { B = _orijinalPikseller[i], G = _orijinalPikseller[i + 1], R = _orijinalPikseller[i + 2] };
                    double enKucukMesafe = double.MaxValue; int enYakinKume = 0;

                    for (int j = 0; j < kumeSayisi; j++)
                    {
                        double mesafe = MahalanobisUzaklikHesapla(piksel, merkezler[j], tersKovaryanslar[j]);
                        mesafe += determinantCezalari[j]; // Varyans yutmasını önler
                        if (mesafe < enKucukMesafe) { enKucukMesafe = mesafe; enYakinKume = j; }
                    }

                    if (etiketler[p] != enYakinKume) { etiketler[p] = enYakinKume; degisimVar = true; }
                    topR[enYakinKume] += piksel.R; topG[enYakinKume] += piksel.G; topB[enYakinKume] += piksel.B;
                    sayac[enYakinKume]++;
                }

                if (!degisimVar) break;

                for (int j = 0; j < kumeSayisi; j++)
                {
                    if (sayac[j] > 0) { merkezler[j].R = topR[j] / sayac[j]; merkezler[j].G = topG[j] / sayac[j]; merkezler[j].B = topB[j] / sayac[j]; }
                    else { int rsg = rastgele.Next(0, toplamPiksel) * 4; merkezler[j] = new RenkVektoru { B = _orijinalPikseller[rsg], G = _orijinalPikseller[rsg + 1], R = _orijinalPikseller[rsg + 2] }; }
                }

                Matris3x3[] yeniKovaryanslar = new Matris3x3[kumeSayisi];
                for (int i = 0, p = 0; i < _orijinalPikseller.Length; i += 4, p++)
                {
                    int k = etiketler[p];
                    RenkVektoru piksel = new RenkVektoru { B = _orijinalPikseller[i], G = _orijinalPikseller[i + 1], R = _orijinalPikseller[i + 2] };
                    double dr = piksel.R - merkezler[k].R; double dg = piksel.G - merkezler[k].G; double db = piksel.B - merkezler[k].B;

                    yeniKovaryanslar[k].M00 += dr * dr; yeniKovaryanslar[k].M01 += dr * dg; yeniKovaryanslar[k].M02 += dr * db;
                    yeniKovaryanslar[k].M10 += dg * dr; yeniKovaryanslar[k].M11 += dg * dg; yeniKovaryanslar[k].M12 += dg * db;
                    yeniKovaryanslar[k].M20 += db * dr; yeniKovaryanslar[k].M21 += db * dg; yeniKovaryanslar[k].M22 += db * db;
                }

                for (int j = 0; j < kumeSayisi; j++)
                {
                    double N = (sayac[j] > 1) ? sayac[j] - 1 : 1;
                    yeniKovaryanslar[j].M00 /= N; yeniKovaryanslar[j].M01 /= N; yeniKovaryanslar[j].M02 /= N;
                    yeniKovaryanslar[j].M10 /= N; yeniKovaryanslar[j].M11 /= N; yeniKovaryanslar[j].M12 /= N;
                    yeniKovaryanslar[j].M20 /= N; yeniKovaryanslar[j].M21 /= N; yeniKovaryanslar[j].M22 /= N;

                    double epsilon = 20.0;
                    yeniKovaryanslar[j].M00 += epsilon; yeniKovaryanslar[j].M11 += epsilon; yeniKovaryanslar[j].M22 += epsilon;

                    double det = MatrisDeterminantHesapla(yeniKovaryanslar[j]);
                    if (det > 0) determinantCezalari[j] = 0.5 * Math.Log(det);
                    else determinantCezalari[j] = 1000;

                    Matris3x3 transpoze = MatrisTranspozesiniAl(yeniKovaryanslar[j]);
                    tersKovaryanslar[j] = GuvenliMatrisTersiAl(transpoze);
                }
            }
            SonuclariIsleVeGoster(etiketler, null, merkezler, iterasyon, null);
        }

        // ==================================================================================
        // --- YENİ ALGORİTMA: EXPECTATION MAXIMIZATION (GMM) ND (DÜZELTİLMİŞ) ---
        // ==================================================================================
        private void ExpectationMaximization_GMM_ND(int kumeSayisi)
        {
            int toplamPiksel = _genislik * _yukseklik;
            int[] etiketler = new int[toplamPiksel];

            // GMM Parametreleri
            RenkVektoru[] merkezler = new RenkVektoru[kumeSayisi];
            Matris3x3[] tersKovaryanslar = new Matris3x3[kumeSayisi];
            double[] determinantlar = new double[kumeSayisi];
            double[] onselOlasiliklar = new double[kumeSayisi]; // Pi_k

            // Sorumluluk Matrisi (Her pikselin her kümeye ait olma olasılığı)
            double[,] sorumluluklar = new double[toplamPiksel, kumeSayisi];

            Random rastgele = new Random(Guid.NewGuid().GetHashCode());

            // 1. BAŞLANGIÇ (INITIALIZATION)
            for (int k = 0; k < kumeSayisi; k++)
            {
                int rsgIndeks = rastgele.Next(0, toplamPiksel) * 4;
                merkezler[k] = new RenkVektoru { B = _orijinalPikseller[rsgIndeks], G = _orijinalPikseller[rsgIndeks + 1], R = _orijinalPikseller[rsgIndeks + 2] };

                // Başlangıçta geniş varyans (Birim Matrisin katları)
                Matris3x3 baslangicKov = new Matris3x3 { M00 = 1000, M11 = 1000, M22 = 1000 };
                tersKovaryanslar[k] = GuvenliMatrisTersiAl(baslangicKov);
                determinantlar[k] = MatrisDeterminantHesapla(baslangicKov);

                onselOlasiliklar[k] = 1.0 / kumeSayisi;
            }

            BaslangicDegerleriniRaporla(null, merkezler);

            int iterasyon = 0;
            int maxIterasyon = 20;
            bool degisimVar = true;

            while (degisimVar && iterasyon < maxIterasyon)
            {
                degisimVar = false;
                iterasyon++;

                // ==========================================
                // E-ADIMI (Expectation): Sorumlulukları Hesapla
                // ==========================================
                // DÜZELTME: i (byte dizisi indeksi, +4 artar), p (piksel indeksi, +1 artar)
                for (int i = 0, p = 0; i < _orijinalPikseller.Length; i += 4, p++)
                {
                    RenkVektoru piksel = new RenkVektoru { B = _orijinalPikseller[i], G = _orijinalPikseller[i + 1], R = _orijinalPikseller[i + 2] };

                    double toplamOlasilik = 0;
                    double[] pikselOlasiliklari = new double[kumeSayisi];

                    for (int k = 0; k < kumeSayisi; k++)
                    {
                        double pdf = GaussOlasilikYogunluguHesapla(piksel, merkezler[k], tersKovaryanslar[k], determinantlar[k]);
                        pikselOlasiliklari[k] = onselOlasiliklar[k] * pdf;
                        toplamOlasilik += pikselOlasiliklari[k];
                    }

                    if (toplamOlasilik <= 0) // Underflow Koruması
                    {
                        double enKucukMesafe = double.MaxValue;
                        int enYakinKume = 0;
                        for (int k = 0; k < kumeSayisi; k++)
                        {
                            double dR = piksel.R - merkezler[k].R;
                            double dG = piksel.G - merkezler[k].G;
                            double dB = piksel.B - merkezler[k].B;
                            double mesafe = (dR * dR) + (dG * dG) + (dB * dB);
                            if (mesafe < enKucukMesafe) { enKucukMesafe = mesafe; enYakinKume = k; }
                            sorumluluklar[p, k] = 0.0;
                        }
                        sorumluluklar[p, enYakinKume] = 1.0;
                    }
                    else
                    {
                        for (int k = 0; k < kumeSayisi; k++)
                        {
                            sorumluluklar[p, k] = pikselOlasiliklari[k] / toplamOlasilik;
                        }
                    }
                }

                // ==========================================
                // M-ADIMI (Maximization): Parametreleri Güncelle
                // ==========================================
                double toplamMerkezDegisimi = 0;

                double[] kumeAgirliklari = new double[kumeSayisi];
                for (int p = 0; p < toplamPiksel; p++)
                {
                    for (int k = 0; k < kumeSayisi; k++) kumeAgirliklari[k] += sorumluluklar[p, k];
                }

                for (int k = 0; k < kumeSayisi; k++)
                {
                    onselOlasiliklar[k] = kumeAgirliklari[k] / toplamPiksel;

                    if (kumeAgirliklari[k] > 0)
                    {
                        double sumR = 0, sumG = 0, sumB = 0;
                        // DÜZELTME YİNE BURADA
                        for (int i = 0, p = 0; i < _orijinalPikseller.Length; i += 4, p++)
                        {
                            sumB += sorumluluklar[p, k] * _orijinalPikseller[i];
                            sumG += sorumluluklar[p, k] * _orijinalPikseller[i + 1];
                            sumR += sorumluluklar[p, k] * _orijinalPikseller[i + 2];
                        }

                        RenkVektoru yeniMerkez = new RenkVektoru
                        {
                            B = sumB / kumeAgirliklari[k],
                            G = sumG / kumeAgirliklari[k],
                            R = sumR / kumeAgirliklari[k]
                        };

                        toplamMerkezDegisimi += Math.Abs(merkezler[k].R - yeniMerkez.R) + Math.Abs(merkezler[k].G - yeniMerkez.G) + Math.Abs(merkezler[k].B - yeniMerkez.B);
                        merkezler[k] = yeniMerkez;
                    }
                }

                for (int k = 0; k < kumeSayisi; k++)
                {
                    if (kumeAgirliklari[k] > 0)
                    {
                        Matris3x3 yeniKov = new Matris3x3();
                        // DÜZELTME YİNE BURADA
                        for (int i = 0, p = 0; i < _orijinalPikseller.Length; i += 4, p++)
                        {
                            double dr = _orijinalPikseller[i + 2] - merkezler[k].R;
                            double dg = _orijinalPikseller[i + 1] - merkezler[k].G;
                            double db = _orijinalPikseller[i] - merkezler[k].B;
                            double yuzde = sorumluluklar[p, k];

                            yeniKov.M00 += yuzde * dr * dr; yeniKov.M01 += yuzde * dr * dg; yeniKov.M02 += yuzde * dr * db;
                            yeniKov.M10 += yuzde * dg * dr; yeniKov.M11 += yuzde * dg * dg; yeniKov.M12 += yuzde * dg * db;
                            yeniKov.M20 += yuzde * db * dr; yeniKov.M21 += yuzde * db * dg; yeniKov.M22 += yuzde * db * db;
                        }

                        yeniKov.M00 /= kumeAgirliklari[k]; yeniKov.M01 /= kumeAgirliklari[k]; yeniKov.M02 /= kumeAgirliklari[k];
                        yeniKov.M10 /= kumeAgirliklari[k]; yeniKov.M11 /= kumeAgirliklari[k]; yeniKov.M12 /= kumeAgirliklari[k];
                        yeniKov.M20 /= kumeAgirliklari[k]; yeniKov.M21 /= kumeAgirliklari[k]; yeniKov.M22 /= kumeAgirliklari[k];

                        double epsilon = 20.0;
                        yeniKov.M00 += epsilon;
                        yeniKov.M11 += epsilon;
                        yeniKov.M22 += epsilon;

                        determinantlar[k] = MatrisDeterminantHesapla(yeniKov);
                        Matris3x3 transpoze = MatrisTranspozesiniAl(yeniKov);
                        tersKovaryanslar[k] = GuvenliMatrisTersiAl(transpoze);
                    }
                }

                if (toplamMerkezDegisimi > 0.5) degisimVar = true;
            }

            // ==========================================
            // SON ETİKETLEME (HARD ASSIGNMENT)
            // ==========================================
            for (int p = 0; p < toplamPiksel; p++)
            {
                double maksSorumluluk = -1;
                int enIyiKume = 0;
                for (int k = 0; k < kumeSayisi; k++)
                {
                    if (sorumluluklar[p, k] > maksSorumluluk)
                    {
                        maksSorumluluk = sorumluluklar[p, k];
                        enIyiKume = k;
                    }
                }
                etiketler[p] = enIyiKume;
            }

            SonuclariIsleVeGoster(etiketler, null, merkezler, iterasyon, null);
        }

        // ==================================================================================
        // BÖLÜM 7: MATEMATİKSEL YARDIMCILAR
        // ==================================================================================

        // YENİ: Gaussian Olasılık Yoğunluk Fonksiyonu (EM için)
        private double GaussOlasilikYogunluguHesapla(RenkVektoru Piksel, RenkVektoru Merkez, Matris3x3 TersMatris, double Determinant)
        {
            // Mahalanobis Uzaklığının Karesi
            double dR = Piksel.R - Merkez.R; double dG = Piksel.G - Merkez.G; double dB = Piksel.B - Merkez.B;
            double t0 = dR * TersMatris.M00 + dG * TersMatris.M10 + dB * TersMatris.M20;
            double t1 = dR * TersMatris.M01 + dG * TersMatris.M11 + dB * TersMatris.M21;
            double t2 = dR * TersMatris.M02 + dG * TersMatris.M12 + dB * TersMatris.M22;
            double kareselSonuc = t0 * dR + t1 * dG + t2 * dB;

            // Katsayı: 1 / sqrt((2pi)^3 * |Sigma|) 
            // (2*pi)^3 yaklaşık 248.050'dir
            double carpan = 1.0 / Math.Sqrt(248.0502134 * Math.Max(Determinant, 1e-9));

            // Sonuç: Katsayı * e^(-0.5 * D^2)
            return carpan * Math.Exp(-0.5 * kareselSonuc);
        }

        private double MatrisDeterminantHesapla(Matris3x3 M)
        {
            return M.M00 * (M.M11 * M.M22 - M.M12 * M.M21) -
                   M.M01 * (M.M10 * M.M22 - M.M12 * M.M20) +
                   M.M02 * (M.M10 * M.M21 - M.M11 * M.M20);
        }

        private Matris3x3 MatrisTranspozesiniAl(Matris3x3 M)
        {
            return new Matris3x3 { M00 = M.M00, M01 = M.M10, M02 = M.M20, M10 = M.M01, M11 = M.M11, M12 = M.M21, M20 = M.M02, M21 = M.M12, M22 = M.M22 };
        }

        private double MahalanobisUzaklikHesapla(RenkVektoru Piksel, RenkVektoru Merkez, Matris3x3 TersMatris)
        {
            double dR = Piksel.R - Merkez.R; double dG = Piksel.G - Merkez.G; double dB = Piksel.B - Merkez.B;
            double t0 = dR * TersMatris.M00 + dG * TersMatris.M10 + dB * TersMatris.M20;
            double t1 = dR * TersMatris.M01 + dG * TersMatris.M11 + dB * TersMatris.M21;
            double t2 = dR * TersMatris.M02 + dG * TersMatris.M12 + dB * TersMatris.M22;
            double kareselSonuc = t0 * dR + t1 * dG + t2 * dB;
            return Math.Sqrt(Math.Max(0, kareselSonuc));
        }

        private Matris3x3 GuvenliMatrisTersiAl(Matris3x3 M)
        {
            double det = MatrisDeterminantHesapla(M);
            if (Math.Abs(det) < 1e-5)
            {
                return new Matris3x3 { M00 = 1.0 / (M.M00 + 1), M11 = 1.0 / (M.M11 + 1), M22 = 1.0 / (M.M22 + 1) };
            }
            double invDet = 1.0 / det;
            return new Matris3x3
            {
                M00 = (M.M11 * M.M22 - M.M12 * M.M21) * invDet,
                M01 = (M.M02 * M.M21 - M.M01 * M.M22) * invDet,
                M02 = (M.M01 * M.M12 - M.M02 * M.M11) * invDet,
                M10 = (M.M12 * M.M20 - M.M10 * M.M22) * invDet,
                M11 = (M.M00 * M.M22 - M.M02 * M.M20) * invDet,
                M12 = (M.M02 * M.M10 - M.M00 * M.M12) * invDet,
                M20 = (M.M10 * M.M21 - M.M11 * M.M20) * invDet,
                M21 = (M.M01 * M.M20 - M.M00 * M.M21) * invDet,
                M22 = (M.M00 * M.M11 - M.M01 * M.M10) * invDet
            };
        }

        // ==================================================================================
        // BÖLÜM 5, 6 & 8: SONUÇ, HİSTOGRAM VE FARE BİLGİSİ
        // ==================================================================================

        private void SonuclariIsleVeGoster(int[] etiketler, double[] T_1D, RenkVektoru[] T_ND, int iterasyon, byte[] histGri)
        {
            int k = (T_1D != null) ? T_1D.Length : T_ND.Length;
            var siraliKumeler = new List<KumeSonucu>();
            for (int i = 0; i < k; i++)
            {
                double yogunluk = (T_1D != null) ? T_1D[i] : (T_ND[i].R + T_ND[i].G + T_ND[i].B) / 3.0;
                byte r, g, b;
                if (T_1D != null) { r = g = b = (byte)Math.Min(255, Math.Max(0, T_1D[i])); }
                else { r = (byte)Math.Min(255, Math.Max(0, T_ND[i].R)); g = (byte)Math.Min(255, Math.Max(0, T_ND[i].G)); b = (byte)Math.Min(255, Math.Max(0, T_ND[i].B)); }
                siraliKumeler.Add(new KumeSonucu { Id = i, Yogunluk = yogunluk, Kirmizi = r.ToString(), Yesil = g.ToString(), Mavi = b.ToString(), GercekKirmizi = r, GercekYesil = g, GercekMavi = b });
            }
            siraliKumeler = siraliKumeler.OrderBy(x => x.Yogunluk).ToList();
            int[] harita = new int[k]; for (int yeni = 0; yeni < k; yeni++) harita[siraliKumeler[yeni].Id] = yeni;
            int[] yeniEtiketler = new int[etiketler.Length]; int[] sayaclar = new int[k];
            for (int i = 0; i < etiketler.Length; i++) { int yeni = harita[etiketler[i]]; yeniEtiketler[i] = yeni; sayaclar[yeni]++; }

            byte[] sonucResim = new byte[_orijinalPikseller.Length];
            for (int i = 0, p = 0; i < sonucResim.Length; i += 4, p++)
            {
                var kume = siraliKumeler[yeniEtiketler[p]];
                sonucResim[i] = kume.GercekMavi; sonucResim[i + 1] = kume.GercekYesil; sonucResim[i + 2] = kume.GercekKirmizi; sonucResim[i + 3] = 255;
            }
            for (int i = 0; i < k; i++) siraliKumeler[i].PikselSayisi = sayaclar[i];
            int[] histData = new int[256];
            if (histGri == null) for (int i = 0, j = 0; i < _orijinalPikseller.Length; i += 4, j++) histData[(byte)(0.299 * _orijinalPikseller[i + 2] + 0.587 * _orijinalPikseller[i + 1] + 0.114 * _orijinalPikseller[i])]++;
            else foreach (byte b in histGri) histData[b]++;
            List<int> ayiricilar = new List<int>(); foreach (var c in siraliKumeler) ayiricilar.Add((int)c.Yogunluk);
            Application.Current.Dispatcher.Invoke(() => {
                ArayuzuGuncelle(sonucResim); HistogramCiz(histData, ayiricilar);
                ResultListView.ItemsSource = null; ResultListView.ItemsSource = siraliKumeler; IterationText.Text = iterasyon.ToString();
            });
        }

        private void ArayuzuGuncelle(byte[] pikseller)
        {
            WriteableBitmap wb = new WriteableBitmap(_genislik, _yukseklik, 96, 96, PixelFormats.Bgra32, null);
            wb.WritePixels(new Int32Rect(0, 0, _genislik, _yukseklik), pikseller, _satirBaytSayisi, 0);
            ProcessedImage.Source = wb; _islenmisPikseller = pikseller;
        }

        private void HistogramCiz(int[] hist, List<int> kirmiziCizgiler)
        {
            HistogramCanvas.Children.Clear(); double maks = hist.Max(); if (maks == 0) return;
            double solB = 40, altB = 25, ustB = 10, sagB = 20;
            double W = HistogramCanvas.ActualWidth > 0 ? HistogramCanvas.ActualWidth : 340;
            double H = HistogramCanvas.ActualHeight > 0 ? HistogramCanvas.ActualHeight : 200;
            double dW = W - solB - sagB; double dH = H - altB - ustB; double barW = dW / 256.0;
            for (int i = 0; i <= 5; i++)
            {
                double val = maks * i / 5.0; double y = ustB + dH - (i * dH / 5.0);
                Line l = new Line { X1 = solB, Y1 = y, X2 = W - sagB, Y2 = y, Stroke = Brushes.LightGray, StrokeThickness = 0.5 };
                HistogramCanvas.Children.Add(l); YaziEkle(val.ToString("F0"), 2, y - 6);
            }
            for (int i = 0; i <= 4; i++)
            {
                int val = i * 64; if (val > 255) val = 255;
                double x = solB + (val * barW);
                Line l = new Line { X1 = x, Y1 = ustB, X2 = x, Y2 = ustB + dH + 5, Stroke = Brushes.Black, StrokeThickness = 0.5 };
                HistogramCanvas.Children.Add(l); YaziEkle(val.ToString(), x - 10, ustB + dH + 5);
            }
            for (int i = 0; i < 256; i++)
            {
                double h = (hist[i] / maks) * dH;
                Rectangle r = new Rectangle { Width = Math.Max(1, barW), Height = h, Fill = Brushes.CornflowerBlue, ToolTip = $"Değer: {i}\nSayı: {hist[i]}" };
                Canvas.SetLeft(r, solB + i * barW); Canvas.SetTop(r, ustB + dH - h); HistogramCanvas.Children.Add(r);
            }
            if (kirmiziCizgiler != null) foreach (int xVal in kirmiziCizgiler)
                {
                    double xPos = solB + xVal * barW;
                    Line l = new Line { X1 = xPos, Y1 = ustB, X2 = xPos, Y2 = ustB + dH, Stroke = Brushes.Red, StrokeThickness = 2 };
                    HistogramCanvas.Children.Add(l);
                    TextBlock tb = new TextBlock { Text = xVal.ToString(), FontSize = 10, Foreground = Brushes.Red, FontWeight = FontWeights.Bold };
                    Canvas.SetLeft(tb, xPos - 8); Canvas.SetTop(tb, ustB + dH + 18); HistogramCanvas.Children.Add(tb);
                }
            Rectangle cerceve = new Rectangle { Width = dW, Height = dH, Stroke = Brushes.Black, StrokeThickness = 1 };
            Canvas.SetLeft(cerceve, solB); Canvas.SetTop(cerceve, ustB); HistogramCanvas.Children.Add(cerceve);
        }

        private void YaziEkle(string metin, double x, double y)
        {
            TextBlock tb = new TextBlock { Text = metin, FontSize = 10, Foreground = Brushes.Black };
            Canvas.SetLeft(tb, x); Canvas.SetTop(tb, y); HistogramCanvas.Children.Add(tb);
        }
        private void BaslangicDegerleriniRaporla(double[] T1, RenkVektoru[] T2)
        {
            Application.Current.Dispatcher.Invoke(() => { StartValuesList.Items.Clear(); if (T2 != null) foreach (var v in T2) StartValuesList.Items.Add($"RGB:{v.R:F0},{v.G:F0},{v.B:F0}"); if (T1 != null) foreach (var v in T1) StartValuesList.Items.Add($"T:{v:F0}"); });
        }

        private void FareBilgisiGuncelle(object sender, MouseEventArgs e, bool tiklandiMi)
        {
            if (_orijinalPikseller == null) return;
            Image img = sender as Image;
            if (img == null && sender is FrameworkElement fe)
            {
                if (fe.Name.Contains("Sol")) img = OriginalImage;
                else if (fe.Name.Contains("Sag")) img = ProcessedImage;
            }
            if (img == null || img.Source == null) return;

            double ctrlW = img.ActualWidth; double ctrlH = img.ActualHeight;
            double imgW = _genislik; double imgH = _yukseklik;

            double scale = Math.Min(ctrlW / imgW, ctrlH / imgH);
            double offsetX = (ctrlW - (imgW * scale)) / 2.0;
            double offsetY = (ctrlH - (imgH * scale)) / 2.0;

            Point pos = e.GetPosition(img);
            int x = (int)((pos.X - offsetX) / scale);
            int y = (int)((pos.Y - offsetY) / scale);

            if (x >= 0 && x < _genislik && y >= 0 && y < _yukseklik)
            {
                int idx = (y * _satirBaytSayisi) + (x * 4);
                byte[] kaynak = (img.Name == "ProcessedImage" && _islenmisPikseller != null) ? _islenmisPikseller : _orijinalPikseller;
                if (kaynak != null && idx + 2 < kaynak.Length)
                {
                    byte B = kaynak[idx]; byte G = kaynak[idx + 1]; byte R = kaynak[idx + 2];
                    string bilgi = $"X:{x} Y:{y} | R:{R} G:{G} B:{B}";
                    if (MouseInfoText != null) MouseInfoText.Text = bilgi;
                    if (tiklandiMi && SelectedPixelInfo != null) SelectedPixelInfo.Text = "Seçilen: " + bilgi;
                }
            }
        }
    }
}
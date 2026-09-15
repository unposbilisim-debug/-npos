<!DOCTYPE html>
<html lang="tr">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>{{ $sayfaBaslik }} — {{ $musteri->Unvan }}</title>
    <style>
        :root { --navy:#1a237e; --orange:#f89d1d; --ink:#1e293b; --muted:#475569; }
        * { box-sizing: border-box; }
        body {
            margin: 0;
            background: #e2e8f0;
            color: var(--ink);
            font-family: "Times New Roman", Times, serif;
            font-size: 12.5pt;
            line-height: 1.45;
        }
        .toolbar {
            position: sticky; top: 0; z-index: 5;
            display: flex; justify-content: flex-end; gap: 8px;
            padding: 10px 16px;
            background: #fff; border-bottom: 1px solid #cbd5e1;
            font-family: system-ui, sans-serif;
        }
        .toolbar button, .toolbar a {
            border: 1px solid #94a3b8; background: #fff; color: #0f172a;
            padding: 6px 14px; border-radius: 6px; cursor: pointer;
            text-decoration: none; font-size: 13px;
        }
        .sheet {
            width: 210mm;
            min-height: 297mm;
            margin: 16px auto 32px;
            background: #fff;
            padding: 18mm 18mm 16mm;
            box-shadow: 0 8px 24px rgba(15,23,42,.12);
        }
        .head {
            display: flex; align-items: flex-end; gap: 0;
            border-bottom: 3px solid var(--navy);
            padding-bottom: 12px;
        }
        .head .brand { display: flex; flex-direction: column; align-items: flex-start; }
        .head img { height: 72px; width: auto; display: block; }
        .head .tagline {
            margin: 2px 0 0;
            font-size: 9pt;
            letter-spacing: .14em;
            text-transform: lowercase;
            color: var(--navy);
            font-family: "Times New Roman", Times, serif;
        }
        .meta {
            display: flex; justify-content: space-between; gap: 16px;
            margin: 14px 0 18px; font-size: 11pt;
        }
        .box { border: 1px solid #cbd5e1; padding: 10px 12px; flex: 1; }
        .box h3 { margin: 0 0 6px; font-size: 10pt; color: var(--navy); text-transform: uppercase; letter-spacing: .08em; }
        .box p { margin: 2px 0; }
        h2.title { text-align: center; font-size: 14pt; margin: 8px 0 16px; }
        .no { text-align: center; font-size: 10pt; color: var(--muted); margin-top: -10px; margin-bottom: 16px; }
        p { text-align: justify; margin: 0 0 8px; }
        ol.madde { padding-left: 1.2em; margin: 0 0 8px; }
        ol.madde > li { margin-bottom: 8px; }
        table.kalem {
            width: 100%; border-collapse: collapse; margin: 8px 0 10px;
            font-size: 11pt;
        }
        table.kalem th, table.kalem td { border: 1px solid #94a3b8; padding: 5px 8px; }
        table.kalem th { background: #f1f5f9; text-align: left; }
        .imza {
            display: flex; gap: 24px; margin-top: 28px;
        }
        .imza .col { flex: 1; text-align: center; }
        .imza .cizgi { margin-top: 48px; border-top: 1px solid #0f172a; padding-top: 6px; }
        .dip { margin-top: 24px; font-size: 9pt; color: var(--muted); }
        @media print {
            body { background: #fff; }
            .toolbar { display: none; }
            .sheet { box-shadow: none; margin: 0; width: auto; min-height: auto; padding: 10mm; }
        }
    </style>
</head>
<body>
@php
    $para = function ($n) { return number_format((float) $n, 2, ',', '.') . ' TL'; };
    $adres = trim(implode(' ', array_filter([$musteri->Adres, $musteri->Ilce, $musteri->Il])));
    $paketOzet = collect($aktifPaketler)->pluck('adi')->filter()->unique()->implode(', ');
    $paketOzet = $paketOzet !== '' ? $paketOzet : 'ilgili yazılım paketi';
    $sureSatirlari = collect($aktifPaketler)->pluck('sure')->filter(fn ($s) => $s && $s !== '—' && $s !== 'Süresiz')->unique();
    $sureOzet = $sureSatirlari->isNotEmpty() ? $sureSatirlari->implode(', ') : null;
    $siparisOzet = $siparisOzet ?? (optional($lisans)->SiparisNo ?: '—');
    $pcOzet = $pcOzet ?? (optional($lisans)->PcName ?: 'işyeri / kasa');
@endphp
<div class="toolbar">
    <a href="{{ url()->previous() }}">Geri</a>
    <button type="button" onclick="window.print()">Yazdır</button>
</div>
<article class="sheet">
    <header class="head">
        <div class="brand">
            <img src="{{ asset('assets/images/unpos-logo.png') }}" alt="ünpos">
            <div class="tagline">yazılım çözümleri</div>
        </div>
    </header>

    <div class="meta">
        <div class="box">
            <h3>Satıcı / Hizmet veren</h3>
            <p><strong>ÜnPOS Bilişim</strong></p>
            <p>Yazılım lisans, bakım ve servis hizmetleri</p>
            <p>Bayi: {{ $bayiAdi }}</p>
        </div>
        <div class="box">
            <h3>Alıcı / Müşteri</h3>
            <p><strong>{{ $musteri->Unvan }}</strong></p>
            @if($musteri->TabelaAdi)<p>Tabela: {{ $musteri->TabelaAdi }}</p>@endif
            <p>VKN: {{ $musteri->VergiNo ?: '—' }} @if($musteri->VergiDairesi) / {{ $musteri->VergiDairesi }}@endif</p>
            <p>{{ $adres ?: '—' }}</p>
            <p>Tel: {{ $musteri->Telefon ?: $musteri->YetkiliGsm ?: '—' }} · E-posta: {{ $musteri->EMail ?: '—' }}</p>
            @if($musteri->Yetkili)<p>Yetkili: {{ $musteri->Yetkili }}</p>@endif
        </div>
    </div>

    <h2 class="title">{{ $sayfaBaslik }}</h2>
    <p class="no">Sözleşme no: {{ $sozlesmeNo }} · Tarih: {{ $tarih }} · Sipariş: {{ $siparisOzet }} · PC: {{ $pcOzet }}</p>

    <table class="kalem">
        <thead>
            <tr>
                <th>Paket adı</th>
                <th>Süre / bitiş</th>
                <th>Tutar</th>
            </tr>
        </thead>
        <tbody>
            @forelse($aktifPaketler as $p)
                <tr>
                    <td>{{ $p['adi'] }}</td>
                    <td>{{ $p['sure'] }}</td>
                    <td>{{ $p['tutar'] > 0 ? $para($p['tutar']) : '—' }}</td>
                </tr>
            @empty
                <tr>
                    <td colspan="3">Bu lisans kaydında aktif paket satırı bulunamadı; konu yine bu sipariş numarasına bağlı yazılımdır.</td>
                </tr>
            @endforelse
            <tr>
                <td colspan="2"><strong>Toplam (liste bedeli, KDV hariç kayıt)</strong></td>
                <td><strong>{{ $toplamTutar > 0 ? $para($toplamTutar) : '—' }}</strong></td>
            </tr>
        </tbody>
    </table>

        <ol class="madde">
            <li><strong>Taraflar.</strong> İşbu Genel Sözleşme bir tarafta ÜnPOS Bilişim (“Satıcı / Hizmet Veren”) ile diğer tarafta yukarıda kimliği yazılı {{ $musteri->Unvan }} (“Müşteri”) arasında {{ $tarih }} tarihinde, bağlı bayi {{ $bayiAdi }} aracılığıyla akdedilmiştir. Sözleşme; yazılım lisansı satışı, bakım/destek ve servis hizmetlerini birlikte kapsar.</li>
            <li><strong>Konu — lisans (satış).</strong> Satıcı, Müşteri’ye {{ $pcOzet }} ortamında kullanmak üzere {{ $paketOzet }} yazılımı için münhasır olmayan, devredilemez bir kullanım hakkı (lisans anahtarı) verir. Kaynak kod, mülkiyet ve marka Satıcı’da kalır. Sipariş(ler): {{ $siparisOzet }}.</li>
            <li><strong>Konu — bakım.</strong> Lisans süresi boyunca Satıcı, mesai saatleri içinde uzaktan destek, hata giderme ve yayımlanan sürüm güncellemelerini sağlar. Yeni modül satışı bu maddenin dışındadır. Müşteri güncel yedek alır ve uzaktan erişime izin verir.</li>
            <li><strong>Konu — servis.</strong> Kurulum, eğitim, yerinde veya uzaktan yapılandırma ve arıza tespiti işçilik hizmeti bu sözleşme kapsamındadır. Yedek parça, mali cihaz, işletim sistemi ve üçüncü kişi cihazları ayrıca faturalanır. Yol ve parça bedeli Müşteri’ye aittir.</li>
            <li><strong>Bedel ve süre.</strong> Bedel, yukarıdaki paket satırlarında gösterilen {{ $toplamTutar > 0 ? $para($toplamTutar) : 'taraflarca teyit edilen' }} tutar esas alınır (KDV hariç kayıt). Ana yazılım paketi süresizdir; bitiş tarihi ve kalan gün uygulanmaz. @if($sureOzet)Yazar kasa paketlerinde süre {{ $sureOzet }} ile sınırlıdır.@else Yazar kasa paketi yoksa veya süresi kayıtlı değilse ayrıca kararlaştırılır.@endif Geciken ödemede ilgili hizmet askıya alınabilir.</li>
            <li><strong>Yükümlülükler.</strong> Müşteri anahtarı üçüncü kişiye vermez, kopyalamaz, tersine mühendislik yapmaz. Donanım, işletim sistemi, internet ve yedekleme Müşteri’nin sorumluluğundadır. Satıcı işi özenle yapar; Müşteri’nin yedeklememesi, yetkisiz müdahalesi veya başka yazılımdan doğan veri kaybından sorumlu değildir. Ayıp ihbarı teslimden itibaren 7 gün içinde yazılı yapılır.</li>
            <li><strong>Gizlilik.</strong> Taraflar; satış, stok, cari, fiş ve kişisel verileri sözleşme süresince ve sonrasında 3 yıl gizli tutar. Destek/servis sırasında görülen veriler yalnızca işin ifası için kullanılır.</li>
            <li><strong>Yetkili mahkeme.</strong> Uygulanacak hukuk T.C. hukukudur. Yetkili mahkeme ve icra daireleri Satıcı’nın faaliyet yeri ile Müşteri’nin ticaret merkezinin bulunduğu yer mahkemeleridir.</li>
        </ol>

    <p>İşbu sözleşme, {{ $tarih }} tarihinde iki nüsha olarak düzenlenmiş ve taraflarca okunarak kabul edilmiştir.</p>

    <div class="imza">
        <div class="col">
            <strong>Satıcı / Hizmet veren</strong>
            <div>ÜnPOS Bilişim</div>
            <div class="cizgi">Kaşe / imza</div>
        </div>
        <div class="col">
            <strong>Alıcı / Müşteri</strong>
            <div>{{ $musteri->Unvan }}</div>
            <div>{{ $musteri->Yetkili ?: 'Yetkili imza' }}</div>
            <div class="cizgi">Kaşe / imza</div>
        </div>
    </div>
    <p class="dip">Bu metin genel çerçeve sözleşmesidir; özel şartlar yazılı ek protokolle değiştirilebilir. Yazdırıp imzalayınız.</p>
</article>
</body>
</html>

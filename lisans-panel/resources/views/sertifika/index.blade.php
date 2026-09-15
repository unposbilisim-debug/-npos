@extends('layouts.app')

@section('content')
<div class="main-content">
    <div class="col-12">
        <div class="card m-b-30">
            <div class="card-body">
                <h4 class="mt-0 header-title">Sertifika Oluştur</h4>
                <p class="text-muted m-b-30 font-14">Müşteri seçin, önizleyin, indirin.</p>
                <div class="custom-alert" style="text-align: center; background: linear-gradient(135deg, #1a237e 0%, #283593 100%); color: white; padding: 15px; border-radius: 5px; margin-bottom: 20px;">
                    Sisteme Firma Logonuzu Yüklemediyseniz Sistem Çalışmaz. Bilgilerim Bölümünden Firma Logonuzu Yüklemeniz Gerek.
                </div>

                <div class="form-group row">
                    <label class="col-sm-1 col-form-label">Müşteri</label>
                    <div class="col-sm-5">
                        <select class="form-select" id="customerSelect">
                            <option value="">Lütfen Müşteri Seçin</option>
                            @foreach ($Musteriler as $geldimusteri)
                            <option value="{{ $geldimusteri->id }}"
                                data-unvan="{{ $geldimusteri->Unvan }}"
                                data-telefon="{{ $geldimusteri->Telefon }}"
                                data-email="{{ $geldimusteri->EMail }}"
                                data-adres="{{ $geldimusteri->Adres }}"
                                data-il="{{ $geldimusteri->Il }}"
                                data-ilce="{{ $geldimusteri->Ilce }}">
                                {{ $geldimusteri->Unvan }}
                            </option>
                            @endforeach
                        </select>
                    </div>

                    <label class="col-sm-1 col-form-label">Program Türü</label>
                    <div class="col-sm-5">
                        <select class="form-select" id="programSelect">
                            <option value="Hızlı Satış">Hızlı Satış</option>
                            <option value="Restaurant Yazılımı">Restaurant Yazılımı</option>
                            <option value="Full Modül">Full Modül</option>
                            <option value="Servis Modülü">Servis Modülü</option>
                            <option value="Ana Nokta Yazılımı">Ana Nokta Yazılımı</option>
                        </select>
                    </div>
                </div>
                <br>
                <h3 style="text-align: center;">Önizleme</h3>
                <hr/>

                <div class="text-center">
                    <canvas id="previewCanvas1" width="710" height="502" style="border:1px solid #ccc; max-width:100%; height:auto;"></canvas>
                    <div class="mt-3">
                        <button type="button" class="btn border-0 text-white" style="background: linear-gradient(135deg, #1a237e 0%, #283593 100%);" id="download1">Sertifikayı İndir</button>
                    </div>
                </div>
            </div>
        </div>
    </div>
</div>

<script>
document.addEventListener('DOMContentLoaded', function() {
    const customerSelect = document.getElementById('customerSelect');
    const programSelect = document.getElementById('programSelect');
    const bayiadi = @json(optional($bayi)->Unvan ?? '');
    const bayilogo = new Image();
    @if(!empty(optional($bayi)->Logo))
    bayilogo.src = "{{ asset($bayi->Logo) }}";
    @endif

    const previewCanvas = document.getElementById('previewCanvas1');
    const previewCtx = previewCanvas.getContext('2d');
    const bg = new Image();
    bg.src = "{{ asset('Sertifika/1.jpg') }}";
    bg.onload = updatePreview;

    function todayStr() {
        const d = new Date();
        return d.getFullYear() + '/' + ('0' + (d.getMonth() + 1)).slice(-2) + '/' + ('0' + d.getDate()).slice(-2);
    }

    function customerData() {
        const opt = customerSelect.options[customerSelect.selectedIndex];
        const il = (opt.dataset.il || '').trim();
        const ilce = (opt.dataset.ilce || '').trim();
        const sehir = [il, ilce].filter(Boolean).join(' / ');
        return {
            name: (opt.dataset.unvan || opt.text || '').trim(),
            phone: (opt.dataset.telefon || '').trim(),
            email: (opt.dataset.email || '').trim(),
            adres: (opt.dataset.adres || '').trim(),
            il: sehir,
            program: programSelect.value,
            date: todayStr()
        };
    }

    function wrapLines(ctx, text, maxWidth, maxLines) {
        const words = String(text || '').split(/\s+/).filter(Boolean);
        const lines = [];
        let cur = '';
        for (const w of words) {
            const test = cur ? (cur + ' ' + w) : w;
            if (ctx.measureText(test).width > maxWidth && cur) {
                lines.push(cur);
                cur = w;
                if (lines.length >= maxLines - 1) {
                    const rest = [w].concat(words.slice(words.indexOf(w) + 1)).join(' ');
                    let last = rest;
                    while (ctx.measureText(last).width > maxWidth && last.length > 4) {
                        last = last.slice(0, -2);
                    }
                    lines.push(last);
                    return lines.slice(0, maxLines);
                }
            } else {
                cur = test;
            }
        }
        if (cur) lines.push(cur);
        return lines.slice(0, maxLines);
    }

    function drawCert(ctx, w, h, data) {
        ctx.clearRect(0, 0, w, h);
        ctx.drawImage(bg, 0, 0, w, h);
        ctx.fillStyle = '#1a1a2e';

        const nameY = h * 0.605;
        const maxNameW = w * 0.58;
        let nameSize = h * 0.048;
        ctx.textAlign = 'center';
        ctx.font = 'bold ' + nameSize + 'px Arial';
        while (ctx.measureText(data.name).width > maxNameW && nameSize > 11) {
            nameSize *= 0.92;
            ctx.font = 'bold ' + nameSize + 'px Arial';
        }
        ctx.fillText(data.name || ' ', w / 2, nameY);

        const colL = w * 0.14;
        const colR = w * 0.54;
        const colW = w * 0.32;
        let y = h * 0.655;
        const lh = h * 0.032;
        const font = Math.max(10, h * 0.022);
        ctx.textAlign = 'left';
        ctx.font = font + 'px Arial';

        ctx.fillText('Program türü: ' + (data.program || ''), colL, y);
        y += lh;
        const adresLines = wrapLines(ctx, 'Adres: ' + (data.adres || '—'), colW, 2);
        adresLines.forEach(function(line) {
            ctx.fillText(line, colL, y);
            y += lh;
        });
        ctx.fillText('Telefon: ' + (data.phone || '—'), colL, y);

        y = h * 0.655;
        ctx.fillText('E-posta: ' + (data.email || '—'), colR, y);
        y += lh;
        ctx.fillText('İl: ' + (data.il || '—'), colR, y);
        y += lh;
        ctx.fillText('Tarih: ' + data.date, colR, y);
        y += lh;
        if (bayiadi) {
            ctx.fillText('Bayi: ' + bayiadi, colR, y);
        }

        if (bayilogo.complete && bayilogo.naturalWidth) {
            const lw = w * 0.11;
            const lhLogo = lw * 0.38;
            ctx.drawImage(bayilogo, w * 0.76, h * 0.655, lw, lhLogo);
        }
    }

    function updatePreview() {
        if (!bg.complete || !bg.naturalWidth) return;
        drawCert(previewCtx, previewCanvas.width, previewCanvas.height, customerData());
    }

    customerSelect.addEventListener('change', updatePreview);
    programSelect.addEventListener('change', updatePreview);
    if (bayilogo.src) bayilogo.onload = updatePreview;

    document.getElementById('download1').addEventListener('click', function() {
        const data = customerData();
        const canvas = document.createElement('canvas');
        canvas.width = 3508;
        canvas.height = 2480;
        drawCert(canvas.getContext('2d'), canvas.width, canvas.height, data);
        const link = document.createElement('a');
        link.download = 'sertifika.png';
        link.href = canvas.toDataURL('image/png');
        link.click();
    });
});
</script>
@endsection

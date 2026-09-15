<style>
  #sidenav li > ul {
    display: none;
    padding-left: 10px;
    list-style: none;
  }

  #sidenav li.is-open > ul {
    display: block;
  }

  .has-arrow:after {
    display: none !important;
  }

  .has-arrow .menu-title {
    display: flex;
    justify-content: space-between;
    align-items: center;
    width: 100%;
  }

  .has-arrow .menu-title:after {
    content: '';
    display: inline-block;
    width: 8px;
    height: 8px;
    border-left: 2px solid #aaa;
    border-bottom: 2px solid #aaa;
    transform: rotate(45deg);
    transition: transform 0.3s ease;
    margin-left: auto;
    flex-shrink: 0;
  }

  .is-open > .has-arrow .menu-title:after {
    transform: rotate(-45deg);
  }
</style>

<div class="sidebar-nav" data-simplebar="true">
  <ul class="metismenu" id="sidenav">

    <li>
      <a href="{{ route('Desk') }}">
        <div class="parent-icon"><i style="color: #3498db" class="material-icons-outlined">home</i></div>
        <div class="menu-title">Ana Sayfa</div>
      </a>
    </li>

    <li>
      <a href="{{ route('priceList') }}">
        <div class="parent-icon"><i style="color: #1abc9c" class="material-icons-outlined">note</i></div>
        <div class="menu-title">Fiyat Listesi</div>
      </a>
    </li>

    <li>
      <a href="{{ route('Dealer') }}">
        <div class="parent-icon"><i style="color: #2ecc71" class="material-icons-outlined">account_balance</i></div>
        <div class="menu-title">Bayi İşlemleri</div>
      </a>
    </li>

    <li>
      <a href="{{ route('Customer') }}">
        <div class="parent-icon"><i style="color: #e74c3c" class="material-icons-outlined">group</i></div>
        <div class="menu-title">Müşteri İşlemleri</div>
      </a>
    </li>

    <li>
      <a class="has-arrow" href="javascript:;">
        <div class="parent-icon"><i style="color: #f39c12" class="material-icons-outlined">apps</i></div>
        <div class="menu-title">Lisans İşlemleri</div>
      </a>
      <ul>
        <li><a href="{{ route('AllLicenses') }}"><i class="material-icons-outlined">schedule</i>Tüm Lisans süreleri</a></li>
        <li><a href="{{ route('UpcomingLicenses') }}"><i class="material-icons-outlined">running_with_errors</i>Yaklaşan Süreler</a></li>
        <li><a href="{{ route('admin.yazarkasa_report') }}"><i class="material-icons-outlined">point_of_sale</i>Yazar Kasa Raporu</a></li>
      </ul>
    </li>
    <li>
      <a href="{{ route('guncelleme.index') }}">
        <i class="material-icons-outlined">system_update</i>
        Güncellemeler
      </a>
    </li>
    <li>
      <a class="has-arrow" href="javascript:;">
        <div class="parent-icon"><i style="color: #1abc9c" class="material-icons-outlined">note</i></div>
        <div class="menu-title">Teklifler</div>
      </a>
      <ul>
        <li><a href="{{ route('Offer') }}"><i class="material-icons-outlined">arrow_right</i>Teklif Oluştur</a></li>
        <li><a href="{{ route('OfferList') }}"><i class="material-icons-outlined">arrow_right</i>Teklif Listesi</a></li>
      </ul>
    </li>

    <li>
      <a href="{{ route('ProgramPackages') }}">
        <div class="parent-icon"><i style="color: #d35400" class="material-icons-outlined">terminal</i></div>
        <div class="menu-title">Program Paketleri</div>
      </a>
    </li>

    <li>
      <a class="has-arrow" href="javascript:;">
        <div class="parent-icon"><i style="color: #8e44ad" class="material-icons-outlined">forum</i></div>
        <div class="menu-title">Bildirim İşlemleri</div>
      </a>
      <ul>
        <li><a href="{{ route('CustomerSMS') }}"><i class="material-icons-outlined">arrow_right</i>Müşteri SMS</a></li>
        <li><a href="{{ route('DealerSMS') }}"><i class="material-icons-outlined">arrow_right</i>Bayi Sms</a></li>
        <li><a href="{{ route('SoftwaresMessage') }}"><i class="material-icons-outlined">arrow_right</i>Program Mesaj</a></li>
        <li><a href="{{ route('PanelMessage') }}"><i class="material-icons-outlined">arrow_right</i>Panel Mesaj</a></li>
        <li><a href="{{ route('LoginMessage') }}"><i class="material-icons-outlined">arrow_right</i>Giriş Mesaj</a></li>
      </ul>
    </li>

    <li>
      <a href="{{ route('Certificate') }}">
        <div class="parent-icon"><i style="color: #16a085" class="material-icons-outlined">quickreply</i></div>
        <div class="menu-title">Sertifika İşlemleri</div>
      </a>
    </li>

    <li>
      <a href="{{ route('Log') }}">
        <div class="parent-icon"><i style="color: #2980b9" class="material-icons-outlined">logo_dev</i></div>
        <div class="menu-title">İşlem Logları</div>
      </a>
    </li>

    <li>
      <a href="{{ route('File') }}">
        <div class="parent-icon"><i style="color: #f1750f" class="material-icons-outlined">cloud_download</i></div>
        <div class="menu-title">Dosya Sistemi</div>
      </a>
    </li>

    <li>
      <a href="{{ route('MicroService') }}">
        <div class="parent-icon"><i style="color: #f1c40f" class="material-icons-outlined">android</i></div>
        <div class="menu-title">Micro Servisler</div>
      </a>
    </li>

    <li>
      <a href="{{ route('Cash') }}">
        <div class="parent-icon"><i style="color: #f10fcb" class="material-icons-outlined">money</i></div>
        <div class="menu-title">Serbest Ödeme</div>
      </a>
    </li>

    <li>
      <a class="has-arrow" href="javascript:;">
        <div class="parent-icon"><i style="color: #e67e22" class="material-icons-outlined">settings</i></div>
        <div class="menu-title">Genel Ayarlar</div>
      </a>
      <ul>
        <li><a href="{{ route('PayTR') }}"><i class="material-icons-outlined">arrow_right</i>PayTR Ayarları</a></li>
        <li><a href="{{ route('SMS') }}"><i class="material-icons-outlined">arrow_right</i>Sms Ayarları</a></li>
      </ul>
    </li>

  </ul>
</div>

<script>
document.addEventListener('DOMContentLoaded', function () {

  // Aktif sayfaya göre menüyü otomatik aç
  const currentUrl = window.location.href;
  document.querySelectorAll('#sidenav li > ul > li > a').forEach(function(link) {
    if (link.href === currentUrl) {
      link.closest('ul').closest('li').classList.add('is-open');
    }
  });

  // Tıklama toggle
  document.querySelectorAll('#sidenav .has-arrow').forEach(function(link) {
    link.addEventListener('click', function(e) {
      e.preventDefault();
      const parentLi = this.closest('li');
      const isOpen = parentLi.classList.contains('is-open');

      // Önce tümünü kapat
      document.querySelectorAll('#sidenav li.is-open').forEach(function(li) {
        li.classList.remove('is-open');
      });

      // Kapalıysa aç
      if (!isOpen) {
        parentLi.classList.add('is-open');
      }
    });
  });

});
</script>
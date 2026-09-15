<!doctype html>
<html lang="tr" data-bs-theme="light">

<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="csrf-token" content="{{ csrf_token() }}">
  <title>ÜnPOS</title>
  
  <link rel="icon" href="{{ asset('assets/images/icon.png') }}" type="image/png">

  <link href="{{ asset('assets/plugins/perfect-scrollbar/css/perfect-scrollbar.css') }}" rel="stylesheet">
  <link rel="stylesheet" type="text/css" href="{{ asset('assets/plugins/metismenu/metisMenu.min.css') }}">
  <link rel="stylesheet" type="text/css" href="{{ asset('assets/plugins/metismenu/mm-vertical.css') }}">
  <link rel="stylesheet" type="text/css" href="{{ asset('assets/plugins/simplebar/css/simplebar.css') }}">
  
  <link href="{{ asset('assets/plugins/datatable/css/dataTables.bootstrap5.min.css') }}" rel="stylesheet" />
  <link href="https://cdn.datatables.net/buttons/2.4.1/css/buttons.bootstrap5.min.css" rel="stylesheet">
  <link href="{{ asset('assets/plugins/sweet-alert2/sweetalert2.min.css') }}" rel="stylesheet" type="text/css">

  <link href="{{ asset('assets/css/bootstrap.min.css') }}" rel="stylesheet">
  <link href="{{ asset('assets/css/bootstrap-extended.css') }}" rel="stylesheet">
  <link href="{{ asset('sass/main.css') }}" rel="stylesheet">
  <link href="{{ asset('sass/dark-theme.css') }}" rel="stylesheet">
  <link href="{{ asset('sass/semi-dark.css') }}" rel="stylesheet">
  <link href="{{ asset('sass/bordered-theme.css') }}" rel="stylesheet">
  <link href="{{ asset('sass/responsive.css') }}" rel="stylesheet">

  <link href="https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@300;400;500;600;700;800&display=swap" rel="stylesheet">
  <link href="https://fonts.googleapis.com/css?family=Material+Icons+Outlined|Material+Icons+Round" rel="stylesheet">
  
  <style>
    :root {
        --primary-color: #f89d1d;
        --primary-dark: #e88612;
        --text-main: #1e293b;
        --text-muted: #64748b;
        --border-color: #e2e8f0;
    }

    body {
        font-family: 'Plus Jakarta Sans', sans-serif;
        background-color: #f8fafc;
    }

    /* --- Navbar & Header --- */
    .top-header .navbar {
        background: #ffffff;
        border-bottom: 1px solid var(--border-color);
        height: 70px;
        box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.01);
        padding: 0 1.5rem;
    }

    /* --- PROFESYONEL BAKİYE KARTI --- */
    .balance-wrapper {
        display: flex;
        align-items: center;
        background-color: #fff;
        border: 1px solid var(--border-color);
        padding: 4px 6px 4px 16px;
        border-radius: 8px;
        height: 44px;
        transition: all 0.2s ease;
    }

    .balance-wrapper:hover {
        border-color: #cbd5e1;
        box-shadow: 0 2px 5px rgba(0,0,0,0.05);
    }

    .balance-content {
        display: flex;
        flex-direction: column;
        justify-content: center;
        line-height: 1.1;
        margin-right: 12px;
        text-align: right;
    }

    .balance-title {
        font-size: 0.65rem;
        text-transform: uppercase;
        color: var(--text-muted);
        font-weight: 700;
        letter-spacing: 0.5px;
    }

    .balance-value {
        font-size: 0.95rem;
        font-weight: 800;
        color: var(--primary-color);
        font-feature-settings: "tnum";
    }

    .balance-icon-box {
        width: 32px;
        height: 32px;
        background-color: #fef5e8;
        color: var(--primary-color);
        border-radius: 6px;
        display: flex;
        align-items: center;
        justify-content: center;
    }

    /* --- Profil Resmi --- */
    .user-profile-img {
        width: 40px; height: 40px;
        object-fit: cover;
        border: 2px solid #fff;
        box-shadow: 0 0 0 1px var(--border-color);
        cursor: pointer;
    }

    /* --- Sidebar Logo & Toggle --- */
    .logo-full { 
        transition: opacity 0.2s ease-in-out; 
        max-width: 160px; 
        height: auto; 
    }

    /* !!! LOGO GİZLEME KURALI (Menü kapandığında) !!! */
    .toggled .logo-full {
        display: none !important;
    }
    
    /* Menü kapandığında header'ı ortala (ikon vs varsa) */
    .toggled .sidebar-header {
        justify-content: center;
    }

    /* --- DataTables --- */
    div.dt-buttons { display: inline-flex; gap: 0.5rem; margin-bottom: 0.5rem; }
    .btn-export {
        display: inline-flex; align-items: center; gap: 5px;
        font-size: 0.85rem; font-weight: 500; padding: 0.4rem 1rem;
        border-radius: 6px !important; border: 1px solid var(--border-color);
        background-color: #fff; color: #475569; transition: all 0.2s;
    }
    .btn-export:hover { background-color: #f8fafc; border-color: #cbd5e1; transform: translateY(-1px); }
    
    /* Input Fix */
    input[type="number"] { -moz-appearance: textfield; }
    input[type="number"]::-webkit-inner-spin-button, input[type="number"]::-webkit-outer-spin-button { -webkit-appearance: none; margin: 0; }
  </style>
</head>

<body>

  <header class="top-header">
    <nav class="navbar navbar-expand align-items-center gap-4">
      
      <div class="btn-toggle" style="cursor: pointer;">
        <a href="javascript:;" class="text-secondary d-flex align-items-center"><i class="material-icons-outlined fs-3">menu</i></a>
      </div>
      
      <ul class="navbar-nav ms-auto gap-3 nav-right-links align-items-center">
        
        @if (Auth::user()->role != 'admin')
        <li class="nav-item d-none d-sm-flex align-items-center">
            <div class="balance-wrapper" title="Hesap Bakiyeniz">
                <div class="balance-content">
                    <span class="balance-title">TOPLAM BAKİYE</span>
                    <span class="balance-value">{{ number_format(Auth::user()->Bakiye ?? 0, 2) }} ₺</span>
                </div>
                <div class="balance-icon-box">
                    <i class="material-icons-outlined fs-5">account_balance_wallet</i>
                </div>
            </div>
        </li>
        @endif

        @php
          $bell = $bugunKuyruguBell ?? ['toplam' => 0, 'onizleme' => []];
          $bellToplam = (int) ($bell['toplam'] ?? 0);
          $bellTur = ['lisans' => 'Lisans', 'yazarkasa' => 'Yazar kasa'];
        @endphp
        <li class="nav-item dropdown d-flex align-items-center">
          <a class="nav-link dropdown-toggle dropdown-toggle-nocaret position-relative text-secondary d-flex align-items-center justify-content-center" data-bs-auto-close="outside"
            data-bs-toggle="dropdown" href="javascript:;" style="width: 40px; height: 40px;" title="Yaklaşan süreler">
            <i class="material-icons-outlined fs-4">notifications</i>
            @if($bellToplam > 0)
              <span class="position-absolute top-0 start-100 translate-middle badge rounded-pill bg-danger" style="font-size: 0.6rem;">{{ $bellToplam > 99 ? '99+' : $bellToplam }}</span>
            @endif
          </a>
          <div class="dropdown-menu dropdown-notify dropdown-menu-end shadow border-0 rounded-4" style="min-width: 280px;">
            <div class="px-3 py-2 d-flex align-items-center justify-content-between border-bottom">
              <h6 class="fw-bold mb-0">Yaklaşan süreler</h6>
              @if((Auth::user()->role ?? null) === 'admin')
                <a href="{{ route('Desk') }}" class="small">Desk</a>
              @endif
            </div>
            <div class="notify-list">
              @forelse(($bell['onizleme'] ?? []) as $oge)
                <a href="{{ $oge['url'] }}" class="d-block px-3 py-2 text-decoration-none text-dark border-bottom">
                  <div class="d-flex justify-content-between gap-2">
                    <span class="fw-semibold text-truncate">{{ $oge['musteri'] }}</span>
                    <span class="small text-muted text-nowrap">{{ $bellTur[$oge['tur']] ?? $oge['tur'] }}</span>
                  </div>
                  <div class="small text-muted text-truncate">
                    {{ $oge['baslik'] }}
                    @if(isset($oge['kalanGun']) && $oge['kalanGun'] < 0)
                      · {{ abs($oge['kalanGun']) }} gün geçti
                    @elseif(isset($oge['kalanGun']))
                      · {{ $oge['kalanGun'] }} gün
                    @endif
                  </div>
                </a>
              @empty
                <div class="px-3 py-3">
                  <p class="mb-0 text-muted small">90 gün içinde yaklaşan lisans veya yazar kasa yok.</p>
                </div>
              @endforelse
            </div>
          </div>
        </li>

        <li class="nav-item dropdown d-flex align-items-center">
          <a href="javascript:;" class="dropdown-toggle dropdown-toggle-nocaret d-flex align-items-center" data-bs-toggle="dropdown">
            <img src="{{ asset('assets/images/icon.png') }}" class="user-profile-img rounded-circle" alt="Profil">
          </a>
          <div class="dropdown-menu dropdown-user dropdown-menu-end shadow border-0 rounded-4 p-0">
            <div class="card border-0 bg-transparent mb-0">
                <div class="card-body p-3 border-bottom">
                    <div class="d-flex align-items-center gap-3">
                        <img src="{{ asset('assets/images/icon.png') }}" class="rounded-circle bg-light p-1" width="50" height="50" alt="">
                        <div>
                            <h6 class="mb-0 fw-bold text-dark">{{ Auth::user()->name }}</h6>
                            <p class="mb-0 text-muted small text-truncate" style="max-width: 150px;">{{ Auth::user()->email }}</p>
                        </div>
                    </div>
                </div>
                <div class="list-group list-group-flush">
                    <a href="{{ route('profile.edit') }}" class="list-group-item list-group-item-action border-0 d-flex align-items-center gap-2 py-2 px-3">
                        <i class="material-icons-outlined fs-5 text-secondary">person</i> Hesabım
                    </a>
                    <form method="POST" action="{{ route('logout') }}">
                        @csrf
                        <a href="{{ route('logout') }}" class="list-group-item list-group-item-action border-0 d-flex align-items-center gap-2 py-2 px-3 text-danger"
                           onclick="event.preventDefault(); this.closest('form').submit();">
                            <i class="material-icons-outlined fs-5">logout</i> Güvenli Çıkış
                        </a>
                    </form>
                </div>
            </div>
          </div>
        </li>

      </ul>
    </nav>
  </header>

  <aside class="sidebar-wrapper" data-simplebar="true">
    <div class="sidebar-header border-bottom">
      <div class="d-flex justify-content-center w-100 py-2">
        <img src="{{ asset('assets/images/logo.png') }}" class="logo-full" alt="Logo">
      </div>
      <div class="sidebar-close d-lg-none">
        <span class="material-icons-outlined">close</span>
      </div>
    </div>
    
    @if (Auth::user()->role == 'admin')
      @include('layouts.adminmenu')
    @elseif (Auth::user()->is_main_dealer)
      @include('layouts.maindealermenu')
    @else
      @include('layouts.bayimenu')
    @endif

    <div class="sidebar-bottom p-3">
      <div class="bg-light rounded p-2 text-center border">
        <small class="text-muted d-block fw-bold" style="font-size: 0.65rem; letter-spacing: 0.5px;">SİSTEM VERSİYONU</small>
        <span class="fw-bold text-dark small">v{{ app()->version() }}</span>
      </div>
    </div>
  </aside>

  <main class="main-wrapper">
    @yield('content')
  </main>

  <div class="overlay btn-toggle"></div>

  <script src="{{ asset('assets/js/bootstrap.bundle.min.js') }}"></script>
  <script src="{{ asset('assets/js/jquery.min.js') }}"></script>
  <script src="{{ asset('assets/plugins/perfect-scrollbar/js/perfect-scrollbar.js') }}"></script>
  <script src="{{ asset('assets/plugins/metismenu/metisMenu.min.js') }}"></script>
  <script src="{{ asset('assets/js/index.js') }}"></script>
  <script src="{{ asset('assets/plugins/peity/jquery.peity.min.js') }}"></script>
  <script> $(".data-attributes span").peity("donut") </script>
  <script src="{{ asset('assets/plugins/simplebar/js/simplebar.min.js') }}"></script>
  
  <script src="{{ asset('assets/plugins/datatable/js/jquery.dataTables.min.js') }}"></script>
  <script src="{{ asset('assets/plugins/datatable/js/dataTables.bootstrap5.min.js') }}"></script>
  <script src="https://cdn.datatables.net/buttons/2.4.1/js/dataTables.buttons.min.js"></script>
  <script src="https://cdn.datatables.net/buttons/2.4.1/js/buttons.bootstrap5.min.js"></script>
  <script src="https://cdnjs.cloudflare.com/ajax/libs/jszip/3.10.1/jszip.min.js"></script>
  <script src="https://cdnjs.cloudflare.com/ajax/libs/pdfmake/0.1.53/pdfmake.min.js"></script>
  <script src="https://cdnjs.cloudflare.com/ajax/libs/pdfmake/0.1.53/vfs_fonts.js"></script>
  <script src="https://cdn.datatables.net/buttons/2.4.1/js/buttons.html5.min.js"></script>
  <script src="https://cdn.datatables.net/buttons/2.4.1/js/buttons.print.min.js"></script>

  <script src="{{ asset('assets/js/main.js') }}"></script>
  <script src="{{ asset('assets/js/ililce.js') }}"></script>
  <script src="{{ asset('assets/plugins/sweet-alert2/sweetalert2.min.js') }}"></script>
  <script src="{{ asset('assets/pages/sweet-alert.init.js') }}"></script>
  <script src="https://cdn.jsdelivr.net/npm/cleave.js@1.6.0/dist/cleave.min.js"></script>
  
  <script>
    $(document).ready(function () {
      
      // Sidebar Toggle
      $(".btn-toggle").on("click", function() {
        if ($(".toggled").length) {
            $("body").removeClass("toggled");
            $(".sidebar-wrapper").hover(function() {
                $("body").addClass("sidebar-hovered");
            }, function() {
                $("body").removeClass("sidebar-hovered");
            });
        } else {
            $("body").addClass("toggled");
            $(".sidebar-wrapper").unbind("mouseenter mouseleave");
        }
      });

      // Overlay Click
      $(".overlay").on("click", function() { $("body").removeClass("toggled"); });

      // DataTables
      $('#example1').DataTable({ "ordering": false });
      var table = $('#example2').DataTable({
        lengthChange: false,
        language: { url: '//cdn.datatables.net/plug-ins/1.13.6/i18n/tr.json' },
        buttons: [
            { extend: 'copy', text: '<i class="material-icons-outlined">content_copy</i> Kopyala', className: 'btn-export' },
            { extend: 'excel', text: '<i class="material-icons-outlined">grid_on</i> Excel', className: 'btn-export' },
            { extend: 'pdf', text: '<i class="material-icons-outlined">picture_as_pdf</i> PDF', className: 'btn-export' },
            { extend: 'print', text: '<i class="material-icons-outlined">print</i> Yazdır', className: 'btn-export' }
        ],
        initComplete: function() {
            $('.dt-button').removeClass('dt-button buttons-html5 buttons-print buttons-copy btn-secondary');
        }
      });
      table.buttons().container().appendTo('#example2_wrapper .col-md-6:eq(0)');

      // Alerts
      @if(session('run_success_js')) swal({ title: 'Başarılı', text: '{{ session('success') }}', type: 'success', timer: 3000 }); @endif
      @if(session('run_error_js')) swal({ title: 'Hata', text: '{{ session('error') }}', type: 'warning', timer: 3000 }); @endif
      @if(session('run_warning_js')) swal({ title: 'Dikkat', text: '{{ session('warning') }}', type: 'warning', timer: 3000 }); @endif
    });
  </script>
</body>
</html>
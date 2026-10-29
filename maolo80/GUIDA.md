# TeknoParrotUI – versione maolo80

Versione modificata di TeknoParrotUI, basata sul commit ufficiale `b75872a`
(22/10/2026, l'ultimo pubblico prima che `teknogods/TeknoParrotUI` diventasse privato).

## Cosa aggiunge

**Giochi NAOMI (TeknoNaomi), nelle Game Settings**
- **Cabinet Outputs:** luci e rinculo delle pistole verso i client MAMEOutput. Attivo di default. Dalla 0.1.0.16 le forze sul volante passano dalle opzioni `--ffb-*`, che qui non sono ancora gestite.
- **Free Play:** imposta il Free Play nel BIOS.
- **BIOS:** vuoto per il BIOS predefinito, oppure un numero (per esempio per un'altra regione).
- **Precise SH-4 FPU:** calcolo in virgola mobile esatto; più lento, utile per glitch di fisica o geometria.
- **Side-by-Side 3D** e **Enable VR:** visore OpenXR, con distanza e larghezza dello schermo, schermo fisso al visore, filtro e controller Touch.
- **Internal Resolution → Auto:** sceglie la scala più alta che entra nel monitor (2 a 1080p, 3 a 1440p, 4 a 2160p). Con il filtro ssaa la raddoppia (8 a 2160p).
- **Presentation Filter** (nearest, linear, bicubic, lanczos, **ssaa**), **CRT Shader** (None, Lottes, Lottes Downsample) e **Use Bezel**: servono TeknoNaomi 0.1.0.16 o successivo, e hanno gli stessi nomi dei profili ufficiali.

**Correzioni generali**
- Il programma non si chiude più all'avvio dopo una ricompilazione (spostamento delle librerie CefSharp).
- Il Controller Setup accetta i tasti anche con il debugger o al primo avvio (finestra attiva al posto di `Windows[0]`).
- I giochi con selettore "Game Revision" non perdono più le impostazioni TeknoNaomi all'avvio.

**Compatibilità con TPUI ufficiale 1.0.0.2267**
- I profili ufficiali nuovi non contengono più `TeknoNaomiControl`. La tabella dei comandi dei 349 profili è inserita nel codice, quindi i giochi con comandi analogici partono anche con i profili ufficiali.
- Le voci ufficiali Region, Outputs e Use Keyboard/Button For Axis non hanno effetto in questa versione, quindi vengono nascoste.

## Branch

| Branch | Uso |
|---|---|
| `teknonaomi-options` | Versione completa da compilare e usare |
| `pr/tpui-fixes` | Solo le correzioni generali (per TeknoGods) |
| `pr/teknonaomi-options` | Solo le opzioni TeknoNaomi (per TeknoGods) |

Nella cartella `maolo80/patches/` ci sono gli stessi commit in formato `git am`, con un README in inglese da consegnare a TeknoGods.

## Compilare

1. **Visual Studio 2026** con il carico di lavoro *Sviluppo di applicazioni desktop .NET* e i pacchetti di destinazione di .NET Framework 4.6.1, 4.6.2 e 4.8.1.
2. Clona `https://github.com/maolo80/TeknoParrotUI.git` e passa al branch `teknonaomi-options`.
3. Apri `TeknoParrotUI.sln` e imposta **Release**.
4. **Compila → Pulisci soluzione**, poi **Compila → Ricompila soluzione**.
5. Il risultato è in `bin\x86\Release\`.

Per provare dal debugger, se il Controller Setup ignora i tasti: *Strumenti → Opzioni → Debug → Ricaricamento rapido XAML* e togli la spunta.
La compilazione Debug apre la finestra "Debug JVS": per giocare usa Release.

## Installare sopra la release ufficiale

1. Installa TeknoParrot ufficiale in una cartella pulita, avvialo e accetta tutti gli aggiornamenti. Poi chiudilo.
2. Fai una copia di sicurezza della cartella.
3. Copia la nostra interfaccia nella cartella vera di TeknoParrot, tenendo i profili e l'emulatore ufficiali. Controlla prima il percorso: robocopy crea la cartella di destinazione se non esiste.

       $d = 'C:\Users\zodia\Desktop\TeknoParrotInstaller_2.0.0.11 (1)'
       Test-Path "$d\TeknoNaomi\TeknoNaomi.exe"   # deve dire True
       robocopy "C:\Users\zodia\source\repos\TeknoParrotUI\bin\x86\Release" "$d" /E /XD GameProfiles Metadata TeknoNaomi
4. Controlla che non ci siano librerie doppie (deve uscire solo "Controllo finito"):

       Get-ChildItem "$d\libs" -Filter *.dll | Where-Object { Test-Path "$d\$($_.Name)" } | ForEach-Object { "DOPPIA: $($_.Name)" }
       "Controllo finito"

5. La nostra interfaccia risulta versione 1.0.0.0, quindi TeknoParrot ripropone l'aggiornamento a ogni avvio. In futuro **rifiuta gli aggiornamenti di TeknoParrotUI**: rimetterebbero la versione ufficiale. Gli aggiornamenti degli emulatori, TeknoNaomi compreso, vanno bene.
6. Per tornare alla versione ufficiale, rimetti Internal Resolution su un numero: la versione ufficiale non conosce "Auto".

## Fluidità di TeknoNaomi (RTX 5070 Ti, monitor 4K)

TeknoNaomi 0.1.0.16 ha `--presentation-filter`, ma non ancora il V-sync: `--vsync` è sconosciuta.

- **Pannello NVIDIA → Impostazioni programma → `TeknoNaomi.exe`:** Sincronizzazione verticale *Attivata*, Risparmio energia *Prestazioni massime*, Latenza bassa *Attivata*, Velocità frame massima *60*.
- **Presentation Filter ssaa con Internal Resolution Auto (8 a 2160p), in Fullscreen.** Con *nearest* le scale più alte del monitor peggiorano l'immagine: 8× su 2160p si vede peggio di 4×. Con *ssaa* invece la migliorano.
- **Ryzen 9 5950X:** se il framerate è irregolare, tieni TeknoNaomi su un solo CCD mentre il gioco gira:

      (Get-Process TeknoNaomi).ProcessorAffinity = 0xFFFF

**Diagnosi.** TPUI non accetta argomenti manuali, quindi si avvia l'emulatore direttamente:

    $n = "C:\TeknoParrot\TeknoNaomi"
    & "$n\TeknoNaomi.exe" --set crzytaxi --rom-root "H:\Crazy Taxi" --state-root "$n\state" --cabinet tpui-1 --play --sh4-recompiler --vulkan-raster --render-scale 4 --vulkan-profile 2>&1 | Tee-Object "$env:USERPROFILE\Desktop\naomi-profilo.txt"

Misure del 26/10/2026 (Crazy Taxi, 8×, 160 s):
- circa 3,5 ms di GPU per scena;
- velocità reale al 98,8%;
- 81 attese `pvr_wait_next_render` da circa 10 ms;
- lavoro PVR più lungo: 36 ms (compilazione dei materiali Vulkan).

## Richiesta a TeknoGods

Il testo completo è in `maolo80/patches/README.md`, in fondo:
1. `--vsync` / `--no-vsync`;
2. un filtro di presentazione (linear, bicubic o riduzione ad area);
3. una cache o precompilazione dei materiali Vulkan PVR.

Quando una nuova versione di TeknoNaomi avrà queste opzioni, si aggiungono in `TeknoParrotUi.Common/TeknoNaomiLaunch.cs` e `TeknoNaomiProfileSettings.cs`, come le altre opzioni.

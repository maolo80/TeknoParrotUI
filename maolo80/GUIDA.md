# TeknoParrotUI – versione maolo80

Versione modificata di TeknoParrotUI, basata sul commit ufficiale `b75872a`
(22/10/2026, l'ultimo pubblico prima che `teknogods/TeknoParrotUI` diventasse privato).

## Cosa aggiunge

**Giochi NAOMI (TeknoNaomi), nelle Game Settings**
- **Cabinet Outputs:** luci, motori e force feedback verso i client MAMEOutput. Attivo di default.
- **Free Play:** imposta il Free Play nel BIOS.
- **BIOS:** vuoto per il BIOS predefinito, oppure un numero (per esempio per un'altra regione).
- **Precise SH-4 FPU:** calcolo in virgola mobile esatto; più lento, utile per glitch di fisica o geometria.
- **Side-by-Side 3D** e **Enable VR:** visore OpenXR, con distanza e larghezza dello schermo, schermo fisso al visore, filtro e controller Touch.
- **Internal Resolution → Auto:** sceglie la scala più alta che entra nel monitor (2 a 1080p, 3 a 1440p, 4 a 2160p).

**Correzioni generali**
- Il programma non si chiude più all'avvio dopo una ricompilazione (spostamento delle librerie CefSharp).
- Il Controller Setup accetta i tasti anche con il debugger o al primo avvio (finestra attiva al posto di `Windows[0]`).
- I giochi con selettore "Game Revision" non perdono più le impostazioni TeknoNaomi all'avvio.

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
3. Copia **tutto** il contenuto di `bin\x86\Release\` nella cartella di TeknoParrot, sostituendo i file. Non copiare solo l'exe e la dll.
4. Controlla che non ci siano librerie doppie (deve uscire solo "Controllo finito"):

       $d = 'C:\TeknoParrot'
       Get-ChildItem "$d\libs" -Filter *.dll | Where-Object { Test-Path "$d\$($_.Name)" } | ForEach-Object { "DOPPIA: $($_.Name)" }
       "Controllo finito"

5. In futuro **rifiuta gli aggiornamenti di TeknoParrotUI**: rimetterebbero la versione ufficiale. Gli aggiornamenti degli emulatori, TeknoNaomi compreso, vanno bene.
6. Per tornare alla versione ufficiale, rimetti Internal Resolution su un numero: la versione ufficiale non conosce "Auto".

## Fluidità di TeknoNaomi (RTX 5070 Ti, monitor 4K)

TeknoNaomi 0.1.0.10 non ha opzioni per V-sync né per il filtro di presentazione: `--vsync`, `--filter` e `--present-filter` sono sconosciute.

- **Pannello NVIDIA → Impostazioni programma → `TeknoNaomi.exe`:** Sincronizzazione verticale *Attivata*, Risparmio energia *Prestazioni massime*, Latenza bassa *Attivata*, Velocità frame massima *60*.
- **Internal Resolution 4 (o Auto) in Fullscreen.** L'emulatore riduce l'immagine con il filtro *nearest*, quindi scale più alte del monitor peggiorano l'immagine: 8× su 2160p si vede peggio di 4×.
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

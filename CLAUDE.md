# WinSense

Controlador para aprimorar o Windows, inspirado nos vídeos de "desbostificar Windows"
(debloat, privacidade, desempenho). O escopo deve crescer além do debloat: a ideia é
ser um painel central de controles do Windows.

## Nome
"WinSense" segue a tradição de utilitários "Win" + nome curto (WinRAR, WinSCP, WinZip).
Conflitos conhecidos e aceitos: Storage Sense, Wi-Fi Sense e o serviço "Sense" do
Microsoft Defender for Endpoint (MsSense.exe) são nomes da Microsoft. Evitar que o
processo ou serviço do WinSense se chame só "Sense".

## Ambiente-alvo do autor
- Windows 11 **Home**, build 26300 (Insider Dev/Canary).
- Home não tem gpedit.msc: tudo via registro, serviços e tarefas agendadas.
- Algumas políticas só valem em Enterprise/Education (ex.: `AllowTelemetry=0`). Na Home,
  compensar desativando serviços e tarefas.
- Build Insider: chaves de registro mudam entre builds. Nunca presumir o estado; sempre
  ler o estado real (`Status()`).

## Hardware do autor
- Acer Nitro V 15 (ANV15-41), BIOS Insyde V1.52, NitroSense instalado.

## Stack
- C# / .NET 10, solução `WinSense.slnx`:
  - `src/WinSense.Core`: lógica, sem interface (net10.0-windows10.0.19041.0 — o SDK do Windows é preciso
    para as APIs WinRT de Bluetooth; System.Management para WMI). App e testes usam o mesmo TFM.
  - `src/WinSense.App`: WPF com o tema Fluent (`ThemeMode="System"`), MVVM com CommunityToolkit.Mvvm.
    Gera `WinSense.exe`. O manifest pede administrador (`requireAdministrator`).
  - `tests/WinSense.Core.Tests`: xUnit. Testar ViewModels com controladores falsos; nunca tocar no firmware em teste.
- Textos da interface e mensagens de erro em português; identificadores do código em inglês.
- Planejado: `WinSense.Cli` e ajustes declarativos em JSON (registro, serviços, tarefas).
- Comandos: `dotnet build WinSense.slnx` · `dotnet test WinSense.slnx` · `dotnet run --project src/WinSense.App`

## Distribuição
- Versão (SemVer) única em `Directory.Build.props`; a interface mostra "Versão X.Y.Z" na barra lateral.
  Ao lançar: subir a versão lá e registrar no `CHANGELOG.md`.
- `.\scripts\publish.ps1` roda os testes e gera em `dist\`: `WinSense-<versão>-setup.exe` (instalador),
  `WinSense-<versão>-win-x64.zip` (portátil) e `SHA256SUMS.txt`. O app é um único `WinSense.exe`
  self-contained (não exige .NET instalado), perfil `Release-win-x64.pubxml`.
- Instalador: Inno Setup 6 (instalado via winget no escopo do usuário), script `installer/WinSense.iss`.
  Instala em `{autopf}\WinSense` e registra o desinstalador em Programas e Recursos.
  **Nunca mudar o `AppId`** (GUID): é por ele que versões novas atualizam a instalação existente.
  Quando houver tweaks com backup, decidir o que o desinstalador faz com eles (hoje não reverte nada).
- O script compila em `artifacts/` (`--artifacts-path`) porque o C# Dev Kit do VS Code builda os mesmos
  projetos em `obj/` ao mesmo tempo e corrompia o build (CS2001 em `*.g.cs`, MSB3030 em `apphost.exe`).
- `RuntimeIdentifiers=win-x64` no App evita re-restore ao alternar entre test e publish (quebra o `_wpftmp` do WPF).
- O `.ps1` e o `.iss` precisam ser UTF-8 **com BOM** (PowerShell 5.1 e Inno Setup leem sem BOM como ANSI).

## Bateria e Carga
- Não existe API do Windows para limite de carga: cada fabricante expõe a sua. Interface
  `IBatteryChargeController` + uma implementação por fabricante, escolhida em `BatteryChargeControllerFactory`.
- Acer: classe WMI `root\WMI:BatteryControl`, métodos `GetBatteryHealthControlStatus` /
  `SetBatteryHealthControl` (função 0x01 = health mode, limite de 80%). Layout dos parâmetros
  tirado do driver Linux acer-wmi-battery. Exige administrador ("Acesso negado" sem ele).
  Depois de gravar, o modo é relido para confirmar.
- A mesma máquina expõe outras classes Acer úteis para controles futuros: `AcerGamingFunction`
  (perfis de desempenho, ventoinhas, LEDs/RGB do teclado) e `AcerBiosConfigurationTool`.

## Monitor do PC (página inicial)
- `WinSense.Core/Monitoring`: `SystemMonitor` (implementa `ISystemMonitor`) lê o inventário uma vez e amostra a cada 1 s.
  - Uso de CPU/GPU via PDH com `PdhAddEnglishCounter` (o Windows do autor é pt-BR e os nomes de contadores
    são traduzidos; caminho em inglês funciona em qualquer idioma). CPU: `% Processor Utility` (igual ao Gerenciador de Tarefas).
  - GPU: nomes e VRAM via DXGI (`Native/Dxgi.cs`); `Win32_VideoController.AdapterRAM` trava em 4 GB. Os contadores
    `GPU Engine` são ligados às GPUs pelo LUID. Uso = motor mais ocupado (soma dos processos por motor), como o Gerenciador.
    Nada cria device D3D: não acordar a RTX 4050 em repouso (Optimus).
  - Integrada × dedicada: heurística por fabricante/nome em `HardwareInventoryReader.ClassifyGpu`.
  - Temperatura: `ITemperatureSource` + `TemperatureSourceFactory`. Acer: `AcerGamingFunction.GetGamingSysInfo`
    (comando `0x01 | sensor << 8`, sensor CPU 0x01 / GPU 0x0A; resultado bits 7:0 status, 23:8 °C — layout do acer-wmi
    do Linux). Fallback: `MSAcpi_ThermalZoneTemperature`. Ambos exigem administrador.
- Início: nome do PC (`Win32_ComputerSystem.DNSHostName`) e modelo (`HardwareInventoryReader.DescribeModel`: família + modelo,
  ignorando placeholders como "Default string"). A interface não diz de onde vêm as temperaturas (pedido do usuário:
  sem referência à marca na tela inicial).
- Máquina do autor: Ryzen 7 7735HS (8C/16T), 1 pente 8 GB DDR5 4800, Radeon integrada + RTX 4050 Laptop (6 GB).
- Ícone: `assets/logo.svg` (mesmo do site) → `scripts/make-icon.ps1` gera `src/WinSense.App/Assets/WinSense.ico/.png`.
- Para ver a interface sem UAC: `dotnet build src/WinSense.App --artifacts-path artifacts/preview -p:ApplicationManifest=`
  (sem admin, temperaturas e bateria aparecem como indisponíveis).

## Teclado
- Num Lock ao ligar: `InitialKeyboardIndicators` (REG_SZ decimal) em `HKU\.DEFAULT` (tela de bloqueio) e `HKCU`.
  Bit 0x2 = Num Lock; preservar os outros bits (0x80000000 é usado com Inicialização Rápida). A opção marcada é a
  da sessão (HKCU); se a tela de bloqueio difere, avisa e oferece "Igualar na tela de bloqueio".
- Teclas F (Fn lock): **removido a pedido do usuário**. Pesquisa (2026-10-11): não há caminho seguro pelo Windows
  neste Acer. `MethodForPowerShell.GetBiosSetting` responde "Control method not support" (só linha comercial);
  `ListBIOSSettings` vazia; `AcerBiosConfigurationTool`/`UtilityFunction`/`DataTrans` gravam blocos sem layout
  conhecido ou criptografados: **não usar**.

## Bluetooth e Dispositivos
- Problema do autor: 2 DualShock 4 (não originais, endereços 41:42:...; Bluetooth clássico, VID 054C PID 09CC).
  O segundo às vezes não reconecta sozinho (o controle é quem chama o PC; se "esquece" o PC, nada acontece).
  Intermitente: no teste de 2026-10-11 ele reconectou sozinho, então **não se sabe ainda qual método resolve**.
- Pedido do usuário: com a chave principal desligada, **nenhum dispositivo aparece**; ligada, aparecem **todos**
  os pareados (Bluetooth clássico) agrupados como nas Configurações (Entrada/Áudio/Outros, pela Class of Device),
  cada um com "Conectar automaticamente". Padrão: controles de jogo marcados, o resto desmarcado. Escolhas por
  endereço em `AppSettings.AutoConnectChoices` (`%LocalAppData%\WinSense\settings.json`).
- `BluetoothAutoConnector` (dono: `App`) roda a cada 8 s, um dispositivo por vez, só os marcados e desconectados.
  `WindowsBluetoothDevices` escolhe o método pelo que o Windows instalou para o dispositivo:
  - **Hid** (nó `BTHENUM\{00001124...}`): A) `GetRfcommServicesAsync(Uncached)` chama o dispositivo (sem admin);
    se respondeu e o HID não subiu, B) `BluetoothSetServiceState` HID off/on (admin; nunca roda se não respondeu,
    para não reinstalar driver em loop).
  - **Audio** (interface KSCATEGORY_AUDIO do A2DP `BTHENUM#{0000110b...}...<endereço>...\SRC`, presente mesmo com
    o fone desconectado): `IOCTL_KS_PROPERTY` com `KSPROPSETID_BtAudio` / `KSPROPERTY_ONESHOT_RECONNECT` (o mesmo
    "Conectar" das Configurações). **Não testado** (conectaria os fones do autor); validar com um fone ligado.
  - **None** (celular etc.): listado, sem caixa habilitada.
- "Conectado": ACL `Connected`; para Hid também o nó BTHENUM com filho `HID\...` presente (`cfgmgr32`) — o nó
  BTHENUM fica "OK" mesmo com o controle desligado.
- No PC do autor a classificação bate com as Configurações: 2 controles (Hid), H02D Pro Anc e Uget-001 (Audio).

## Ícones ocultos e início com o Windows
- `TrayIcon` usa o `NotifyIcon` do Windows Forms (`UseWindowsForms` só para isso; `<Using Remove>` tira
  `System.Windows.Forms`/`System.Drawing` dos usings implícitos para não conflitar com o WPF).
- `ShutdownMode=OnExplicitShutdown`. X esconde a janela (aviso em balão uma vez por execução); "Sair" no menu
  do ícone encerra. `OnSessionEnding` libera o fechamento (logoff/desligar e o Restart Manager do instalador).
- Instância única por sessão: mutex `Local\WinSense.Instance`; a segunda cópia sinaliza o evento
  `Local\WinSense.ShowWindow` e sai. Só cópias **elevadas** chegam a esse ponto (a não elevada se relança antes),
  então não há problema de permissão nos objetos nomeados.
- Início com o Windows: `StartWithWindows` cria a tarefa `\WinSense\Iniciar com o Windows (<SID>)` com gatilho
  de logon do usuário, atraso de 5 s, `--background` e RunLevel Highest (sem UAC). Só em Program Files.
  XML validado com schtasks no PC do autor. Página "Configurações" liga/desliga.
- Instalador: tarefa "startup" (marcada) roda `WinSense.exe --register-startup` (ou `--unregister-startup` se
  desmarcada) elevado; o app cria as tarefas e sai sem abrir janela.
- Teste automatizado do fluxo (sem admin): `--background` sem janela; segunda abertura mostra a janela da
  primeira; WM_CLOSE esconde e o processo continua. Script em scratchpad `traytest.ps1` (refazer se precisar).

## Elevação e janela
- Manifest `asInvoker`. `App.OnStartup`: se não está elevado, roda a tarefa `\WinSense\Abrir WinSense (<SID>)`
  (`schtasks /run`, sem UAC) ou, se não existir, se relança com `runas` (UAC). Elevado, cria/atualiza a tarefa
  (`ElevatedStartupTask.EnsureCreated`) **só se o exe estiver em Program Files** (senão seria escalada de privilégio).
  O desinstalador remove as tarefas de `\WinSense\`. XML validado com schtasks no PC do autor.
- `WINSENSE_NO_ELEVATE=1` pula a elevação (só para ver a interface em desenvolvimento) e usa nomes próprios
  de instância única (`.Dev`), para rodar ao lado do WinSense instalado. Cuidado: usa o mesmo `settings.json`.
- Janela: `WindowPlacement` salva tamanho/posição/maximizada em `%LocalAppData%\WinSense\window.json` ao fechar;
  descarta a posição se o monitor não existe mais.

## Diagnósticos elevados
- `Start-Process powershell -Verb RunAs -Wait` com um script **meu, somente leitura**, que grava a saída num arquivo.
  O classificador de permissões **bloqueia executar programas baixados** (ex.: HackBGRT) elevado: não insistir.
- `AcerGamingFunction.GetGamingSysInfo` confirmado: 0x0101 CPU °C, 0x0201 ventoinha CPU RPM, 0x0301 sensor do
  sistema °C, 0x0601 ventoinha GPU RPM, 0x0A01 GPU °C (0 com a RTX em repouso). Ventoinhas ainda não estão no monitor.

## Logo de inicialização (pesquisa, ainda não implementado)
- Imagem atual: `NtQuerySystemInformation(SystemBootLogoInformation=140)` devolve o BMP da BGRT **sem admin**
  (no PC do autor: "acer NITRO" 534×432, 24 bits). Vale para qualquer marca UEFI.
- Acer (nativo, preferido): o serviço do NitroSense (`AcerAgentService`/`AcerHardwareService`) copia `AcerLogo.jpg`
  ou `.gif` para `\EFI\OEM\` da partição EFI; o BIOS lê no POST. Restaurar = apagar. Suporte via SMBIOS
  (`get_customize_post_from_smbios`). No PC do autor `\EFI\OEM` ainda não existe. **Pendente:** o usuário aplicar
  um logo pelo NitroSense para observarmos nome/formato/limites antes de implementar.
- Genérico: HackBGRT (MIT, v2.6.0) troca a BGRT com um loader EFI antes do Windows; arriscado (BitLocker, PIN,
  anti-cheat, boot). Se usado, só como opção "Arriscado" opt-in para marcas sem método nativo.

## Estado atual (2026-10-11)
- Feito: solução criada; seção "Bateria e Carga" com "Modo de Carregamento de Bateria"
  (Carregamento Otimizado = limite de 80% / Carregamento Completo = 100%). Build ok, 5 testes passando.
- **v1.0.0** (ainda não lançada; o usuário quer tudo junto na 1.0.0, **não subir a versão** sem ele pedir):
  monitor do PC, bateria e carga, teclado (Num Lock), ícone, instalador, abrir sem UAC, lembrar a janela.
  Temperatura da CPU (Acer) confirmada elevada. Num Lock: a opção marcada é a da sessão (HKCU); se a tela de
  bloqueio (.DEFAULT) difere, avisa e oferece "Igualar na tela de bloqueio".
- **Pendente:** validar no hardware real. A chamada WMI da Acer não foi testada elevada; o usuário vai rodar
  `dotnet run --project src/WinSense.App` como administrador e comparar com o NitroSense.
  Se falhar, revisar o layout dos parâmetros em `AcerBatteryChargeController`.
- Ainda não há repositório git (perguntar antes de iniciar).
- Próximas ideias: controles via `AcerGamingFunction` (perfis, ventoinhas, RGB); itens 🟢 de docs/TWEAKS.md.

## Arquitetura dos ajustes
- Cada ajuste (tweak) é um módulo com:
  - `Id`, `Nome`, `Descricao`, `Categoria`, `Risco` (Seguro / Atencao / Arriscado)
  - `Aplicar()`, `Reverter()`, `Status()`
- Antes de aplicar, salvar o valor original (backup) para permitir reverter de verdade.
- Criar um ponto de restauração antes de lotes de alterações.
- Ajustes "Arriscado" são sempre opt-in, com aviso explícito.
- Perfis (ex.: `basico`, `privacidade`, `gamer`) = listas de Ids de tweaks.
- PowerShell embutido (runspace) só como escape, para o que só existe como cmdlet.

## Catálogo de ajustes
Ver [docs/TWEAKS.md](docs/TWEAKS.md).

## Referências (ferramentas que os vídeos usam)
- Chris Titus Tech WinUtil — https://github.com/ChrisTitusTech/winutil
- O&O ShutUp10++ — https://www.oo-software.com/en/shutup10
- Win11Debloat (Raphire) — https://github.com/Raphire/Win11Debloat
- Sophia Script — https://github.com/farag2/Sophia-Script-for-Windows
- AtlasOS / ReviOS (distros modificadas, referência de tweaks agressivos)

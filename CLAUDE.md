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
  - `src/WinSense.Core`: lógica, sem interface (net10.0-windows, System.Management para WMI).
  - `src/WinSense.App`: WPF com o tema Fluent (`ThemeMode="System"`), MVVM com CommunityToolkit.Mvvm.
    Gera `WinSense.exe`. O manifest pede administrador (`requireAdministrator`).
  - `tests/WinSense.Core.Tests`: xUnit. Testar ViewModels com controladores falsos; nunca tocar no firmware em teste.
- Textos da interface e mensagens de erro em português; identificadores do código em inglês.
- Planejado: `WinSense.Cli` e ajustes declarativos em JSON (registro, serviços, tarefas).
- Comandos: `dotnet build WinSense.slnx` · `dotnet test WinSense.slnx` · `dotnet run --project src/WinSense.App`

## Bateria e Carga
- Não existe API do Windows para limite de carga: cada fabricante expõe a sua. Interface
  `IBatteryChargeController` + uma implementação por fabricante, escolhida em `BatteryChargeControllerFactory`.
- Acer: classe WMI `root\WMI:BatteryControl`, métodos `GetBatteryHealthControlStatus` /
  `SetBatteryHealthControl` (função 0x01 = health mode, limite de 80%). Layout dos parâmetros
  tirado do driver Linux acer-wmi-battery. Exige administrador ("Acesso negado" sem ele).
  Depois de gravar, o modo é relido para confirmar.
- A mesma máquina expõe outras classes Acer úteis para controles futuros: `AcerGamingFunction`
  (perfis de desempenho, ventoinhas, LEDs/RGB do teclado) e `AcerBiosConfigurationTool`.

## Estado atual (2026-10-11)
- Feito: solução criada; seção "Bateria e Carga" com "Modo de Carregamento de Bateria"
  (Carregamento Otimizado = limite de 80% / Carregamento Completo = 100%). Build ok, 5 testes passando.
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

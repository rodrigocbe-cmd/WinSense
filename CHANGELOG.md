# Changelog

Versões seguem o [SemVer](https://semver.org/lang/pt-BR/): `MAIOR.MENOR.CORREÇÃO`.

## [1.0.0] - 2026-10-11
Primeira versão de distribuição.

### Adicionado
- Página inicial com o **nome e o modelo do PC** e o **Monitor do PC**, atualizado a cada segundo:
  - Processador (CPU): nome, núcleos e threads, uso, velocidade atual e temperatura.
  - Memória (RAM): capacidade, tipo (DDR4, DDR5...), velocidade, quantidade de pentes e uso.
  - Placas de vídeo (GPU): cada uma com o nome, se é dedicada ou integrada, uso e memória de vídeo.
  - Gráfico do último minuto de uso de cada componente.
  - Temperaturas da CPU e da GPU pelos sensores do fabricante (por enquanto, Acer); em outros
    computadores, a zona térmica ACPI do Windows, quando existir.
- Seção **Bateria e Carga** com o **Modo de Carregamento de Bateria**:
  - Carregamento Otimizado: a carga para em 80% para preservar a bateria.
  - Carregamento Completo: a bateria carrega até 100%.
  - Suporte a notebooks Acer (interface WMI `BatteryControl`, a mesma do NitroSense).
  - Exibição da carga atual e de quando o notebook está na tomada.
- Seção **Bluetooth e Dispositivos** com **Conectar dispositivos automaticamente**:
  - Desligada, a página não mostra nenhum dispositivo. Ligada, lista todos os dispositivos pareados,
    agrupados como nas Configurações do Windows (Entrada, Áudio, Outros), com o status de cada um.
  - Cada dispositivo tem a sua caixa **Conectar automaticamente** (controles vêm marcados; fones não).
  - Enquanto o WinSense estiver rodando, procura a cada poucos segundos os dispositivos marcados que
    estiverem desconectados e conecta os que estiverem ligados (para dispositivos que "esquecem" o PC,
    como alguns DualShock 4). Fones usam o mesmo "Conectar" das Configurações do Windows.
  - Histórico das últimas tentativas.
- Seção **Teclado** com **Num Lock ao ligar o PC**: ligado ou desligado. Mostra a opção em uso e
  avisa quando a tela de bloqueio começa diferente, com um botão para igualar.
- A janela abre do jeito que foi fechada: maximizada ou no mesmo tamanho e posição.
- **Ícone nos ícones ocultos** (perto do relógio): fechar a janela no X mantém o WinSense rodando;
  para encerrar, use **Sair** no menu do ícone. Abrir o WinSense de novo só mostra a janela que já existe.
- **Iniciar com o Windows**, já nos ícones ocultos e sem o aviso do UAC: opção no instalador
  (marcada por padrão) e em **Configurações**.
- Tema Fluent que segue o tema claro/escuro do Windows.
- Ícone do WinSense (o mesmo logo do site) no programa, na janela e no instalador.
- Instalador (`WinSense-1.0.0-setup.exe`): instala em Arquivos de Programas, cria atalho no menu
  Iniciar (e, opcionalmente, na área de trabalho) e registra o desinstalador em Programas e Recursos
  e em Configurações > Aplicativos.
- Versão portátil (`.zip`) para quem preferir não instalar.

### Observações
- Precisa de permissão de administrador. Instalado, o WinSense abre sem o aviso do UAC (usa uma
  tarefa do Agendador de Tarefas criada na primeira execução e removida ao desinstalar).
  A versão portátil pede a permissão a cada abertura.
- Windows 10 (versão 2004 ou mais nova) ou Windows 11, x64, sem precisar instalar o .NET.
- Desinstalar o WinSense não altera o modo de carregamento: o limite de carga fica gravado no firmware.
  Para voltar a 100%, escolha "Carregamento Completo" antes de desinstalar.

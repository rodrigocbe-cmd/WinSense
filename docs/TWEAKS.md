# Catálogo de ajustes

Levantamento do que os vídeos de "desbostificar Windows" e as ferramentas que eles usam
(WinUtil, O&O ShutUp10++, Win11Debloat, Atlas/ReviOS, Sophia Script) habilitam e desabilitam.

**Risco:** 🟢 seguro · 🟡 tem efeito colateral · 🔴 arriscado (opt-in, com aviso)

Status de implementação: `[ ]` pendente · `[x]` implementado

## 1. Telemetria e privacidade
- [ ] 🟢 Desativar telemetria (`AllowTelemetry=0`, serviços **DiagTrack** e **dmwappushservice**)
- [ ] 🟢 Desativar ID de publicidade
- [ ] 🟢 Desativar "Experiências personalizadas", dicas e sugestões
- [ ] 🟢 Desativar histórico de atividades / Timeline
- [ ] 🟡 Desativar rastreamento de localização
- [ ] 🟢 Desativar coleta de escrita e digitação (inking/typing)
- [ ] 🟢 Desativar feedback automático do Windows
- [ ] 🟢 Desativar tarefas agendadas de telemetria (CEIP, Compatibility Appraiser)
- [ ] 🟡 Desativar o relatório de erros (WER)

## 2. IA e Copilot
- [ ] 🟢 Desativar o Copilot e esconder o botão
- [ ] 🟢 Desativar o Recall / Windows AI
- [ ] 🟢 Desativar Click to Do e as ações de IA no Explorer
- [ ] 🟢 Desativar o Copilot no Edge, no Paint e no Bloco de Notas

## 3. Bloatware (apps pré-instalados)
- [ ] 🟢 Remover Candy Crush, TikTok, Spotify, Disney+, Clipchamp, News, Weather, Solitaire, Teams pessoal, Get Help, Dicas, Mapas, Pessoas etc.
- [ ] 🟢 Impedir a reinstalação automática (`ContentDeliveryManager`, `SilentInstalledAppsEnabled`)
- [ ] 🟡 Remover o OneDrive
- [ ] 🟡 Remover o Xbox Game Bar e os serviços do Xbox (quebra o Game Pass)
- [ ] 🔴 Remover o Edge (o WebView2 depende dele, e as atualizações reinstalam)
- [ ] 🔴 Remover a Microsoft Store

## 4. Interface: Iniciar, barra de tarefas e Explorer
- [ ] 🟢 Remover Widgets, Notícias e Interesses
- [ ] 🟢 Remover a pesquisa Bing/web do menu Iniciar
- [ ] 🟢 Esconder os botões de Chat, Task View e Pesquisa
- [ ] 🟢 Alinhar a barra de tarefas à esquerda
- [ ] 🟢 Restaurar o menu de contexto clássico (o do Windows 10)
- [ ] 🟢 Mostrar extensões de arquivo e arquivos ocultos
- [ ] 🟢 Abrir o Explorer em "Este Computador"
- [ ] 🟢 Remover "Recomendados" e anúncios do Iniciar
- [ ] 🟢 Desativar anúncios na tela de bloqueio (Spotlight)
- [ ] 🟢 Tema escuro
- [ ] 🟢 Desativar o Sticky Keys (Shift 5x)
- [ ] 🟢 Remover a Galeria e a Página Inicial do Explorer

## 5. Desempenho
- [ ] 🟡 Plano de energia Alto Desempenho / Ultimate Performance (notebook gasta mais bateria)
- [ ] 🟡 Desativar apps em segundo plano (notificações de alguns apps param)
- [ ] 🟢 Reduzir efeitos visuais e animações
- [ ] 🟢 Desativar a Inicialização Rápida (Fast Startup)
- [ ] 🟡 Desativar a hibernação
- [ ] 🟡 Desativar o SysMain / Superfetch (só faz sentido em HDD)
- [ ] 🟡 Desativar a indexação do Windows Search (a busca fica lenta)
- [ ] 🟢 Ativar o Agendamento de GPU acelerado por hardware
- [ ] 🟢 Ativar o Game Mode e desativar a aceleração do mouse
- [ ] 🟢 Desativar o Game DVR
- [ ] 🟡 Mudar serviços para Manual em vez de Automático (lista do WinUtil)
- [ ] 🔴 Desativar mitigações de Spectre/Meltdown
- [ ] 🔴 Desativar a VBS / Integridade de Memória

## 6. Windows Update
- [ ] 🟢 Adiar as atualizações de recursos (365 dias) e as de segurança (alguns dias)
- [ ] 🟡 Impedir a instalação automática de drivers pelo Windows Update
- [ ] 🟢 Desativar a Otimização de Entrega (P2P)
- [ ] 🟢 Impedir a reinicialização automática com usuário logado
- [ ] 🔴 Desativar o Windows Update por completo

## 7. Segurança (vídeos mais agressivos)
- [ ] 🟢 Desativar o envio de amostras do Defender
- [ ] 🔴 Desativar o SmartScreen
- [ ] 🔴 Desativar o Defender
- [ ] 🔴 Desativar o UAC

## 8. Rede
- [ ] 🟢 Trocar o DNS (Cloudflare, Google, Quad9)
- [ ] 🟡 Desativar o IPv6 (às vezes quebra coisas)
- [ ] 🟢 Desativar o Wi-Fi Sense e o hotspot automático
- [ ] 🟡 Desativar o algoritmo de Nagle (para jogos)

## 9. Utilidades
- [ ] Limpar arquivos temporários, o cache do Windows Update e Prefetch, e rodar `DISM /StartComponentCleanup`
- [ ] Reparo: `sfc /scannow` e `DISM /RestoreHealth`
- [ ] Instalar programas em lote via **winget**
- [ ] Criar um ponto de restauração antes de qualquer alteração
- [ ] Instalar sem conta Microsoft (`OOBE\BYPASSNRO`)

## 10. Bateria e Carga
- [x] 🟢 Modo de Carregamento de Bateria: Otimizado (limite de 80%) ou Completo (100%). Por enquanto só Acer.

## 11. Controles extras (a definir)
Espaço para os controles além do debloat que ainda vão ser definidos.

# WinSense

Controlador para deixar o Windows mais limpo, privado e rápido, inspirado nos vídeos de
"desbostificar Windows".

Cada ajuste pode ser **aplicado**, **revertido** e **consultado** (status), e o valor original
é salvo antes de qualquer alteração.

> Status: em planejamento. O catálogo de ajustes está em [docs/TWEAKS.md](docs/TWEAKS.md).

## Princípios
- Nada irreversível: todo ajuste tem `Reverter()`.
- Ler o estado real do sistema, nunca presumir.
- Ajustes arriscados (🔴) são sempre opt-in.
- Compatível com o Windows 11 Home (sem gpedit).

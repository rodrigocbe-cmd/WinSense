# WinSense

Controlador para deixar o Windows mais limpo, privado e rápido, inspirado nos vídeos de
"desbostificar Windows".

Cada ajuste pode ser **aplicado**, **revertido** e **consultado** (status), e o valor original
é salvo antes de qualquer alteração.

> Versão atual: **1.0.0** (ver [CHANGELOG.md](CHANGELOG.md)). O catálogo de ajustes está em [docs/TWEAKS.md](docs/TWEAKS.md).

## Instalação
Execute `WinSense-<versão>-setup.exe`. Ele instala em `C:\Program Files\WinSense`, cria o atalho no
menu Iniciar e registra o desinstalador em **Painel de Controle > Programas e Recursos** (e em
Configurações > Aplicativos). Não precisa instalar o .NET.

Versão portátil: extraia `WinSense-<versão>-win-x64.zip` e execute `WinSense.exe`.

O Windows pede permissão de administrador ao abrir (necessária para conversar com o firmware).

## Gerar uma versão
Requer o [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`).
```powershell
.\scripts\publish.ps1   # testes + dist\: setup.exe, .zip portátil e SHA256SUMS.txt
```
A versão fica em `Directory.Build.props`.

## Princípios
- Nada irreversível: todo ajuste tem `Reverter()`.
- Ler o estado real do sistema, nunca presumir.
- Ajustes arriscados (🔴) são sempre opt-in.
- Compatível com o Windows 11 Home (sem gpedit).

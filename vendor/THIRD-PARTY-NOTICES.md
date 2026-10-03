# Third-party components

SteamFixVN 1.2 starts an unmodified external GoodbyeDPI executable. It does not link GoodbyeDPI or WinDivert into the C# application or modify their source/binaries.

## GoodbyeDPI 0.2.2

- Copyright ValdikSS and contributors; Apache License 2.0. Preserve the notices shipped in `licenses/LICENSE-goodbyedpi.txt`, `LICENSE-getline.txt`, and `LICENSE-uthash.txt`.
- Binary origin: https://github.com/ValdikSS/GoodbyeDPI/releases/download/0.2.2/goodbyedpi-0.2.2.zip
- Original downloaded archive SHA-256: `00A2F8B99CD817F8C7FC4C449033015F039D18AF213DE78CB66BF202277C0628`.
- The package embeds only the original x86_64 executable, WinDivert DLL/driver and all four upstream license files. No upstream service installation scripts or country-wide hostname lists are included.
- Exact GoodbyeDPI source tag is provided in `third-party-sources/GoodbyeDPI-0.2.2-source.zip`: https://github.com/ValdikSS/GoodbyeDPI/tree/0.2.2

## WinDivert dependency

- Copyright Basil Projects and contributors; GNU Lesser General Public License v3, including the accompanying GPL text, preserved in `licenses/LICENSE-windivert.txt`.
- The GoodbyeDPI 0.2.2 build workflow references WinDivert 1.4.3-A: https://github.com/ValdikSS/GoodbyeDPI/blob/0.2.2/.github/workflows/build.yml
- Corresponding WinDivert source tag is provided in `third-party-sources/WinDivert-1.4.3-source.zip`: https://github.com/basil00/WinDivert/tree/v1.4.3
- User-mode DLL/driver are distributed unchanged, alongside their source archive and licenses. Engine files are extracted separately, and can be inspected/rebuilt with the upstream source and build instructions. SteamFixVN pins the embedded package checksum; a developer replacing the engine should rebuild the app and update the checksum rather than silently substituting a different privileged binary.

No affiliation with Valve, ValdikSS, or Basil Projects is implied. See upstream license texts for warranties and redistribution terms.

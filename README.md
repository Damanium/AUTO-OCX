# AUTO OCX

Registra o desregistra varios controles ActiveX (`.ocx`, `.dll`, `.ax`) a la vez con `regsvr32`.
Detecta si cada archivo es de 32 o 64 bits y usa el `regsvr32` correcto. Pide permisos de administrador (UAC) al abrirse.

- **Versión:** 1.1.0
- **Autor:** Juan Carlos Alvarez Oncala — juankylodk@gmail.com
- **Licencia:** MIT. Software gratuito y de uso libre (ver [LICENSE](LICENSE)).

## Uso
1. Ejecuta `AutoOCX.exe` y acepta el aviso de administrador.
2. Arrastra los archivos o usa "Añadir archivos…" / "Añadir carpeta…".
3. Pulsa "Registrar todo" o "Desregistrar".

## Compilar
Ejecuta `build.bat` (usa el compilador de .NET Framework 4.x incluido en Windows).

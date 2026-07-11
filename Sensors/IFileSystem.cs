namespace CoreTempLinux.Sensors;

/// <summary>
/// Acceso de solo lectura al sistema de archivos, tolerante a fallos.
/// <para>
/// Los lectores de sensores dependen de esta abstracción (y no de <see cref="File"/>
/// o <see cref="Directory"/> directamente) por dos motivos: desacopla la lógica de
/// los sensores del sistema real <c>/sys</c>-<c>/proc</c> (invirtiendo la dependencia,
/// lo que además permite sustituirla en pruebas) y concentra en un único lugar el
/// manejo de errores: ninguna implementación debe lanzar excepciones, sino degradar
/// a un valor vacío o nulo dejando constancia en el registro.
/// </para>
/// </summary>
public interface IFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    /// <summary>Contenido del archivo ya recortado, o <c>null</c> si no se pudo leer.</summary>
    string? ReadText(string path);

    /// <summary>Líneas del archivo, o una lista vacía si no se pudo leer.</summary>
    IReadOnlyList<string> ReadLines(string path);

    /// <summary>Subdirectorios de <paramref name="path"/>, o una lista vacía.</summary>
    IReadOnlyList<string> GetDirectories(string path);

    /// <summary>Archivos de <paramref name="dir"/> que casan con el patrón, o vacía.</summary>
    IReadOnlyList<string> GetFiles(string dir, string pattern);
}

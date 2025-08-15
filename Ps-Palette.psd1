@{
    GUID = '5df9e918-2d62-41ba-bd11-0d6ca05148e7'

    # The name of the DLL that contains your cmdlets
    RootModule = 'Ps-Palette.dll' # The name of your DLL file

    ModuleVersion = '1.0.0'
    Author = 'Øyvind Nilsen'
    Description = 'A module to extract color palettes from images.'

    # The cmdlets to export from the module
    CmdletsToExport = @(
        'Get-ImagePalette'
    )
}
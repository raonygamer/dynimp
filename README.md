Program that enables dynamic importing based on *.dynimp.json files. (Meant for Minecraft Bedrock Edition)
To use dynmap.exe download the latest release and use: dynmap --help
Example *.dynimp.json file:
```json
{
    // Portable Executable to point imports at runtime, can be any portable executable file.
    "module": "my_module.dll",
    // Imports can be functions/data
    "imports": [
        {
            // The exact symbol of the dynamic import
            "symbol": "MyDynamicImport",
            // The point of the dynamic import
            "points": [
                {
                    // The version constraint of the executable to point the import at, 
                    // if the running executable version isn't equal, 
                    // it will omit this import from the runtime, to avoid this, you can use "any"
                    "version": "1.14.60.5",
                    // The type of the point, can be "address" or "signature"
                    "type": "address",
                    // The value of the point, can be a hex string with wildcards (signature) 
                    // or a hex number (address)
                    // if address is used, it will be added to the base of the module address at 
                    // runtime.
                    "value": "0x12345678"
                }
            ]
        }
    ]
}

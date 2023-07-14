# PDB_Downloader

## Authors

- [@grandgi24u](https://www.github.com/grandgi24u)

## Installation of PDB_Downloader

- Install pdbDownloader on your computer 
- Open it with Visual Studio
- Open file "app.config" and choose your parameter :

```xml
  <appSettings>
    <add key="outDir" value="../../../outDir/" /> // if using file output choose output dir
    <add key="outFileName" value="output" /> // if using file output choose output file name
    <add key="bestFileName" value="best" /> // if using file output choose output best file name
    <add key="useSql" value="true" /> // if you want to use a sql database : true | if not (use file) : false
    <add key="sqlConnection" value="Data Source=localhost;Initial Catalog=MmCifFiles; Integrated Security=True"/> // if using sql database output url to sql database
    <add key="numberToCheck" value="500"/> // how file to check for each launch (recommand : max 500)
  </appSettings>
```

- Compile the program
- Launch it with .exe or inside Visual Studio




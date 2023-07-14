# PDB_Downloader

## Authors

- [@grandgi24u](https://www.github.com/grandgi24u)

## Project Description

This C# software provides a powerful tool for retrieving and analyzing molecular data. It offers two modes of operation: file mode and database mode.

### File Mode

In file mode, the software utilizes files for processing. It performs the following steps:

1. Requests a list of file names from the API at [link to api](https://search.rcsb.org/).
2. Checks whether each file already exists in either the database or a JSON file that it creates.
3. Allows you to specify the number of files to check (usually 500).
4. Continues making API requests until it has added the desired number of new files to its dataset.
5. Retrieves the clashscore for each file by making requests to the API at [link to api](https://files.rcsb.org/pub/pdb/validation_reports/).
6. Obtains the name of the molecule associated with each file from the API at [link to api](https://data.rcsb.org/rest/v1/core/entry/).

### Database Mode

In database mode, the software interacts with a SQL Server database. The specific operations and functionality in this mode will be tailored to your database setup and requirements.

After executing the software, you will have an enriched dataset containing x+500 files, each with its corresponding clashscore and molecule name stored in either the database or a JSON file.

## Getting Started

To get started with the software, follow these steps:

- Clone the repository to your local machine.
- Install the necessary dependencies and configure your environment.
- Choose the desired mode of operation (file mode or database mode) and configure the software accordingly (see config section).
- Compile and run the software.
- Check the generated dataset in either the database or the JSON file, depending on the chosen mode.
- Please refer to the code comments for more detailed instructions and customization options.

### Config app

To get started with the software and configure its parameters, you can modify the app.config file. Here's an explanation of the configurable parameters:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <startup>
    <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.7.2" />
  </startup>
  <appSettings>
    <add key="outDir" value="../../../outDir/" />
    <add key="outFileName" value="output" />
    <add key="bestFileName" value="best" />
    <add key="useSql" value="true" />
    <add key="sqlConnection" value="Data Source=localhost;Initial Catalog=MmCifFiles; Integrated Security=True"/>
    <add key="numberToCheck" value="500"/>
  </appSettings>
</configuration>
```

- **outDir**: Specifies the output directory where the generated files will be stored. Modify the value attribute to specify the desired output directory path.
- **outFileName**: Sets the name of the output file. Modify the value attribute to set your preferred output file name.
- **bestFileName**: Defines the name of the file that contains the best results. Modify the value attribute to set the desired name for this file.
- **useSql**: Determines whether the software should use SQL Server database mode. If set to "**true**", the software will utilize the SQL Server database specified in the sqlConnection parameter. If set to "**false**", the software will use file mode. Modify the value attribute to switch between the two modes.
- **sqlConnection**: Specifies the connection string for the SQL Server database. Modify the value attribute to provide the appropriate connection string based on your database configuration.
- **numberToCheck**: Determines the number of files to check. Modify the value attribute to set the desired number of files to check during the execution of the software. (Recommand : max 500)

Ensure that you save the modified app.config file before compiling and running the software. These parameters will be read by the software at runtime and used according to your configurations.


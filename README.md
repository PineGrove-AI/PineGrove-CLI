# PineGrove CLI

Welcome to the PineGrove CLI! 
This CLI wraps vllm and its dependencies for a seamless, clean setup for running LLMs
to support your PineGrove application.


## Installation 

```bash
curl -fsSL https://git.getpinegrove.eu/pinegrove-community/pinegrove-cli/raw/branch/main/install.sh | bash
```

The installer downloads the CLI binary, creates a self-contained Python runtime, and installs vllm into it. CUDA version is detected automatically. No manual Python or venv setup required.


## Quickstart
To get started, simply run the following command in your terminal to generate an initial config file:

```bash 
pinegrove-cli init
```

Then, edit your file to contain your favourite models and runtime args!

Afterwards, simply run the following command to start your server:

```bash
pinegrove-cli start
```

To stop it, run:
```bash
pinegrove-cli stop 
```

## Status & Logs
To view the status of your PineGrove server, you can use the following command:

```bash
pinegrove-cli status
```

To view the logs of your PineGrove server, you can use the following command:

```bash
pinegrove-cli logs <model-name>
```
This will stream the logs from the specified model.


## Development & Building the CLI

To build the CLI (for Linux), run the following command:
```bash
dotnet publish src/Pinegrove.Cli/Pinegrove.Cli.csproj -c Release -r linux-x64 \
  --self-contained true \
  /p:PublishSingleFile=true
```
This will output `src/Pinegrove.Cli/bin/Release/net10.0/linux-x64/publish/pinegrove-cli`.

### Running the CLI

You might need to make the output executable by running the following command:

```bash
chmod +x pinegrove-cli 
```

Then simply run the CLI using the following command:

```bash
./pinegrove-cli <command>
```

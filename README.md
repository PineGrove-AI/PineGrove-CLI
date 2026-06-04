# PineGrove CLI

Welcome to the PineGrove CLI! 

This CLI translates a simple config file into fully functional vllm docker containers, allowing you to easily run and manage 
LLMs optimized for your PineGrove application, and with all dependencies handled for you.

This config can be built through the CLI itself guiding you through model selection based on your use-case and hardware via `pinegrove-cli init`.

## Prerequisites

This CLI requires:
- a linux machine with an NVIDIA GPU 
- Docker installed with access to said GPU (usually via the NVIDIA Container Toolkit)

## Installation 

```bash
curl -fsSL https://git.getpinegrove.eu/pinegrove-community/pinegrove-cli/raw/branch/main/install.sh | bash
```

The installer downloads the CLI binary, checks for relevant dependencies based on your hardware, and adds the CLI to your PATH. 


## Quickstart
Run the following command to generate an initial config file:

```bash 
pinegrove-cli init
```

Then, edit your file to contain your favourite models and runtime args!

Afterwards, simply run the following command to start your server:

```bash
pinegrove-cli start
```
This will spin up Docker containers for each of the models specified in your config file, and start serving them on the specified ports.

To stop it, run:
```bash
pinegrove-cli stop 
```

## Status & Logs
Status and logs can be accessed via Docker in the usual ways (`docker ps`, `docker logs <container-id>`, etc.), but the CLI also provides some convenient commands to access this information.

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

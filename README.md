![LEAN Data Source SDK](http://cdn.quantconnect.com.s3.us-east-1.amazonaws.com/datasources/Github_LeanDataSourceSDK.png)

# Lean ThetaData DataSource Plugin

[![Build & Test](https://github.com/QuantConnect/Lean.DataSource.ThetaData/actions/workflows/gh-actions.yml/badge.svg)](https://github.com/QuantConnect/Lean.DataSource.ThetaData/actions/workflows/gh-actions.yml)

This library, built on .NET 10, connects the QuantConnect LEAN engine to ThetaData. It uses the ThetaData REST API v3 through Theta Terminal v3 (default port 25503).

- [MIGRATION.md](MIGRATION.md): what changed from API v2, the configuration keys, and what each plan allows.

## Introduction
ThetaData Library is an open-source project written in C#, designed to simplify the process of accessing real-time and historical financial market data. With support for Options Data across all exchanges and low latency, it offers a comprehensive solution for algorithmic trading.

## Features
### Easy Integration with QuantConnect LEAN Algorithmic Trading Engine
Seamlessly incorporate ThetaData into your trading strategies within the QuantConnect LEAN environment.

### Rich Financial Data
Access a wealth of financial data including real-time and historical information. Subscribe to different option contracts with various expiry dates, strikes, and rights.

### Flexible Configuration
Customize the library to suit your needs with flexible configuration options.

### Symbol SecurityType Support
#### Historical Data
- [x] Equity
- [x] Equity Option
- [x] Index
- [x] Index Option
#### Real-time Updates
- [x] Equity
- [x] Equity Option
- [x] Index
- [x] IndexOption
### Backtesting and Research
Utilize the power of QuantConnect.LEAN CLI to test and optimize your trading algorithms in both backtest and research modes.

## Getting Started
You can use the following command line arguments to launch the [LEAN CLI](https://github.com/quantConnect/Lean-cli) pip project with ThetaData. For more detailed information, refer to the [ThetaData](https://www.quantconnect.com/docs/v2/lean-cli/datasets/theta-data) documentation.

Each command needs `--thetadata-rest-url http://host.docker.internal:25503`: the CLI's built-in default is the retired v2 port 25510.

### Downloading Data

```
lean data download --data-provider-historical ThetaData --data-type Trade --resolution Daily --security-type Option --ticker NVDA,AMD --start 20240303 --end 20240404 --thetadata-subscription-plan Standard --thetadata-rest-url http://host.docker.internal:25503
```
### Backtesting
```
lean backtest "My Project" --data-provider-historical ThetaData --thetadata-subscription-plan Standard --thetadata-rest-url http://host.docker.internal:25503
```
### Jupyter Research Notebooks
```
lean research "My Project" --data-provider-historical ThetaData --thetadata-subscription-plan Standard --thetadata-rest-url http://host.docker.internal:25503
```
### Live Trading
```
lean live deploy "My Project" --data-provider-live ThetaData --thetadata-subscription-plan Standard --brokerage "Paper Trading"
``` 

## Contributing
Contributions to the project are highly encouraged! Feel free to open issues, submit pull requests, or contribute in any way you see fit.

## Installation
To contribute to the ThetaData API Connector Library for .NET 10 within QuantConnect LEAN, follow these steps:
1. Install and start [Theta Terminal v3](https://thetadata.net/docs/Articles/Getting-Started/Getting-Started.html).
2. Fork the Project: Fork the repository by clicking the "Fork" button at the top right of the GitHub page.
3. Clone Your Forked Repository:
```
https://github.com/QuantConnect/Lean.DataSource.ThetaData.git
```
4. Configuration:
- [optional] Set the thetadata-subscription-plan (by default: Free)
```
{
  "thetadata-subscription-plan": ""
}
```

## Price Plan
For detailed information on ThetaData's pricing plans, please refer to the [ThetaData Pricing](https://www.thetadata.net/subscribe) page.

## Documentation
ThetaData API v3 documentation: https://thetadata.net/docs/Articles/Getting-Started/Getting-Started.html

## License
This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.

Happy coding and algorithmic trading! 📈💻

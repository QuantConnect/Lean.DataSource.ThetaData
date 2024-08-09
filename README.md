![LEAN Data Source SDK](http://cdn.quantconnect.com.s3.us-east-1.amazonaws.com/datasources/Github_LeanDataSourceSDK.png)

# Lean ThetaData DataSource Plugin

[![Build & Test](https://github.com/QuantConnect/Lean.DataSource.ThetaData/actions/workflows/gh-actions.yml/badge.svg)](https://github.com/QuantConnect/Lean.DataSource.ThetaData/actions/workflows/gh-actions.yml)

Welcome to the ThetaData Library repository! This library, built on .NET 6, provides seamless integration with the QuantConnect LEAN Algorithmic Trading Engine. It empowers users to interact with ThetaData's financial dataset to create powerful trading algorithms.

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
- [x] Index - [support tickers list](https://http-docs.thetadata.us/docs/theta-data-rest-api-v2/s1ezbyfni6rw0-index-option-tickers)
- [x] IndexOption - [support tickers list](https://http-docs.thetadata.us/docs/theta-data-rest-api-v2/s1ezbyfni6rw0-index-option-tickers)
### Backtesting and Research
Utilize the power of QuantConnect.LEAN CLI to test and optimize your trading algorithms in both backtest and research modes.

## Getting Started
You can use the following command line arguments to launch the [LEAN CLI](https://github.com/quantConnect/Lean-cli) pip project with ThetaData. For more detailed information, refer to the [ThetaData](https://www.quantconnect.com/docs/v2/lean-cli/datasets/theta-data) documentation.

### Downloading Data

```
lean data download --data-provider-historical ThetaData --data-type Trade --resolution Daily --security-type Option --ticker NVDA,AMD --start 20240303 --end 20240404 --thetadata-subscription-plan Standard
```
### Backtesting
```
lean backtest "My Project" --data-provider-historical ThetaData --thetadata-subscription-plan Standard
```
### Jupyter Research Notebooks
```
lean research "My Project" --data-provider-historical ThetaData --thetadata-subscription-plan Standard
```
### Live Trading
```
lean live deploy "My Project" --data-provider-live ThetaData --thetadata-subscription-plan Standard --brokerage "Paper Trading"
``` 

## Contributing
Contributions to the project are highly encouraged! Feel free to open issues, submit pull requests, or contribute in any way you see fit.

## Installation
To contribute to the ThetaData API Connector Library for .NET 6 within QuantConnect LEAN, follow these steps:
1. Obtain [ThetaData client](https://http-docs.thetadata.us/docs/theta-data-rest-api-v2/4g9ms9h4009k0-getting-started) and follow thier instaction to run client.
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
For detailed documentation on how to use ThetaData Library, please visit [documentation](https://http-docs.thetadata.us/docs/theta-data-rest-api-v2/4g9ms9h4009k0-getting-started).

## License
This project is licensed under the MIT License - see the [LICENSE](https://github.com/QuantConnect/Lean.DataSource.ThetaData/blob/master/LICENSE) file for details.

Happy coding and algorithmic trading! 📈💻

---
description: "Financial domain knowledge, terminology, and design patterns for Capital Markets, Banking, and Insurance applications"
applyTo: "**"
---

# Financial Domain Standards

All solutions target regulated financial services. Apply domain knowledge consistently
across naming, data modeling, agent design, and UI language.

---

## Capital Markets

### Core Concepts

- **Instrument** — A financial security: equity (stock), fixed income (bond), derivative (option, future, swap), structured product
- **Portfolio** — A collection of positions held by a client or fund
- **Position** — A holding of a specific instrument (quantity, cost basis, market value, weight)
- **Trade** — A buy or sell order for an instrument
- **Settlement** — The actual transfer of securities and cash after a trade
- **Benchmark** — A reference index to measure portfolio performance (e.g., S&P 500, Bloomberg Aggregate)
- **Alpha** — Excess return above benchmark; **Beta** — Correlation/sensitivity to market moves
- **Drawdown** — Peak-to-trough decline in portfolio value
- **VaR (Value at Risk)** — Statistical measure of maximum expected loss at a confidence level
- **Duration** — Sensitivity of a bond's price to interest rate changes
- **Spread** — Yield differential between a bond and a risk-free benchmark

### Identifiers

| Identifier | Standard | Example |
|---|---|---|
| Equity | CUSIP (US), ISIN (international), SEDOL (UK) | US0378331005 |
| Bond | ISIN | US912810RZ78 |
| Derivative | Bloomberg ticker | AAPL US 01/17/25 C200 |
| Fund | CUSIP / ISIN | — |
| Exchange | MIC code | XNAS (NASDAQ) |
| Currency | ISO 4217 | USD, EUR, GBP |

### Agent Design Patterns

- **PortfolioAnalysisAgent** — Analyzes current holdings, risk, and concentration
- **MarketIntelligenceAgent** — Synthesizes news, earnings, and macro data for holdings
- **BacktestingAgent** — Simulates historical portfolio performance
- **RebalanceAgent** — Generates optimal rebalancing trades given constraints
- **RiskAdvisoryAgent** — Computes risk metrics (VaR, stress tests, factor exposures)
- **PortfolioConstructionAgent** — Builds portfolios from universe with given objectives/constraints

### Key Regulations

- **MiFID II** (Europe) — Best execution, client suitability, transaction reporting
- **SEC Reg BI** (US) — Best interest standard for broker-dealers
- **FINRA** — US broker-dealer regulation
- **Basel III/IV** — Bank capital and liquidity requirements
- **EMIR** — European Market Infrastructure Regulation (derivatives)

---

## Banking

### Core Concepts

- **KYC (Know Your Customer)** — Client identity verification and due diligence
- **AML (Anti-Money Laundering)** — Detecting and preventing money laundering
- **Credit Risk** — Risk of borrower default
- **LTV (Loan-to-Value)** — Loan amount as % of collateral value
- **PD (Probability of Default)**, **LGD (Loss Given Default)**, **EAD (Exposure at Default)** — Credit risk components
- **NPA (Non-Performing Asset)** — Loan in default or near default
- **CRAR (Capital to Risk-Weighted Assets Ratio)** — Bank's capital adequacy ratio
- **Treasury** — Manages a bank's balance sheet, liquidity, and interest rate risk
- **DSCR (Debt Service Coverage Ratio)** — Borrower's ability to repay debt

### Agent Design Patterns

- **KYCAgent** — Validates customer identity, runs sanctions screening
- **CreditScoringAgent** — Evaluates creditworthiness from financial data
- **FraudDetectionAgent** — Identifies suspicious transactions in real time
- **LoanOriginationAgent** — Automates loan application processing
- **ComplianceAgent** — Checks transactions against AML/KYC rules
- **TreasuryAgent** — Analyzes liquidity position and ALM (Asset-Liability Management)

### Key Regulations

- **Basel III/IV** — Capital, liquidity (LCR, NSFR), and leverage ratios
- **DORA** (Europe) — Digital Operational Resilience Act
- **SOX** (US) — Financial reporting controls
- **GDPR** / **CCPA** — Personal data protection
- **FATF** — Anti-money laundering standards

---

## Insurance

### Core Concepts

- **Policy** — Contract between insurer and insured
- **Premium** — Payment made by insured to insurer
- **Claim** — Request for payment under a policy
- **Underwriting** — Process of evaluating risk and setting premium
- **Actuarial** — Statistical analysis for risk pricing and reserving
- **Reinsurance** — Insurance bought by insurers to transfer risk
- **Loss Ratio** — Claims paid ÷ Premiums earned
- **Combined Ratio** — (Claims + Expenses) ÷ Premiums; < 100% = profitable
- **Catastrophe Modeling** — Quantifying risk from natural disasters or large events
- **IBNR (Incurred But Not Reported)** — Claims that have occurred but not yet been filed

### Insurance Types

- **P&C (Property & Casualty)** — Home, auto, commercial property
- **Life & Annuity** — Term life, whole life, variable annuity
- **Health** — Individual, group, specialty
- **Specialty** — Marine, aviation, cyber, professional liability

### Agent Design Patterns

- **UnderwritingAgent** — Evaluates risk profile and suggests premium/coverage
- **ClaimsTriageAgent** — Classifies and routes incoming claims
- **FraudDetectionAgent** — Identifies potentially fraudulent claims
- **ActuarialAgent** — Projects loss reserves and risk metrics
- **PolicyRecommendationAgent** — Recommends coverage adjustments based on life events
- **ReinsuranceAnalysisAgent** — Evaluates reinsurance treaty optimization

### Key Regulations

- **Solvency II** (Europe) — Capital requirements for insurance companies
- **NAIC** (US) — National Association of Insurance Commissioners standards
- **IFRS 17** — International insurance contract accounting standard

---

## Cross-Domain Patterns

### Data Privacy in Financial Services

Never store or log:
- Full account numbers (mask as `****1234`)
- Social Security Numbers / Tax IDs (mask completely)
- Date of birth (use age range instead where possible)
- Full names in conjunction with financial data in logs
- Credit card numbers (PCI-DSS compliance)
- Biometric data

### Audit Trail Requirements

Every financial data mutation must log:
```python
{
    "event_type": "portfolio_rebalance",
    "timestamp": "2024-01-15T10:30:00Z",
    "advisor_id": "adv_12345",
    "client_id": "cli_67890",
    "session_id": "sess_abc",
    "action": "rebalance_submitted",
    "metadata": {"portfolio_id": "pf_xyz", "trade_count": 5},
    "ip_address": "10.x.x.x",   # Internal IP only
    "user_agent": "...",
}
```

### Human-in-the-Loop Gates

Financial applications require human approval before executing consequential actions:
- Before submitting trades or rebalancing
- Before updating client risk profiles
- Before sending client communications
- Before large claims payouts

In the UI, human gates appear as **amber/orange** nodes in the workflow visualization.
In the backend, represent gates as state machine transitions requiring explicit approval.

### Suitability and Recommendation

When an agent makes a financial recommendation:
1. Always include **rationale** with supporting data
2. Always include **risk warnings** appropriate to the client profile
3. Always include **source attribution** (news article, earnings report, etc.)
4. Log the recommendation with a compliance audit record
5. Never recommend specific securities without suitability checks

### Financial Terminology in Naming

Prefer these patterns in code:

| Generic | Financial |
|---|---|
| `user` | `client`, `advisor`, `counterparty` |
| `item` | `instrument`, `position`, `holding` |
| `transaction` | `trade`, `settlement`, `claim`, `premium` |
| `group` | `portfolio`, `fund`, `policy` |
| `metric` | `risk_score`, `alpha`, `duration`, `loss_ratio` |
| `event` | `market_event`, `credit_event`, `claim_event` |

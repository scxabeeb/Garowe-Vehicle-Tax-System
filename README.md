# Garowe Vehicle Tax System

## Golis Bill Query API

This project provides the endpoint requested by the Golis team:

- `GET /api/golis/queryBillInfo`
- `POST /api/golis/queryBillInfo`

Production URL:

- `https://www.tax.garowecity.pl.so/api/golis/queryBillInfo`

### Request Modes

Use either of the following:

1. `invoiceNumber`
2. `plateNumber` + `movement`

If both are provided, `invoiceNumber` is used first.

### Authentication

The endpoint enforces authentication based on configuration:

1. Basic Auth is required if `GolisWebhook:ApiUsername` and `GolisWebhook:ApiPassword` are configured.
2. `X-Golis-Secret` header is required if `GolisWebhook:Secret` is configured.

If these values are empty, the matching check is skipped.

---

## Examples

### 1) GET by invoice number

```bash
curl -X GET "https://www.tax.garowecity.pl.so/api/golis/queryBillInfo?invoiceNumber=26071801" \
	-H "Authorization: Basic <base64(username:password)>" \
	-H "X-Golis-Secret: <your-secret>"
```

### 2) POST by invoice number

```bash
curl -X POST "https://www.tax.garowecity.pl.so/api/golis/queryBillInfo" \
	-H "Content-Type: application/json" \
	-H "Authorization: Basic <base64(username:password)>" \
	-H "X-Golis-Secret: <your-secret>" \
	-d '{
		"invoiceNumber": "26071801"
	}'
```

### 3) POST by plate + movement

```bash
curl -X POST "https://www.tax.garowecity.pl.so/api/golis/queryBillInfo" \
	-H "Content-Type: application/json" \
	-H "Authorization: Basic <base64(username:password)>" \
	-H "X-Golis-Secret: <your-secret>" \
	-d '{
		"plateNumber": "GAR-1234",
		"movement": "Entry"
	}'
```

### 4) POST using Golis wrapped payload

The endpoint also accepts the payload structure used by Golis integrations where values are nested inside `requestBody`.

```bash
curl -X POST "https://www.tax.garowecity.pl.so/api/golis/queryBillInfo" \
	-H "Content-Type: application/json" \
	-d '{
		"requestBody": {
			"billNumber": "26071801",
			"invoiceId": "26071801"
		},
		"requestHeader": {
			"apikey": "garowetax"
		}
	}'
```

---

## Success Response (Example)

```json
{
	"requestId": "EzQMJVjN",
	"schemaVersion": "1.0",
	"responseHeader": {
		"timestamp": "20260718114500001",
		"resultCode": "0",
		"resultMessage": "SUCCESS"
	},
	"billInfo": [
		{
			"billId": "1",
			"billTo": "OWNER NAME",
			"billAmount": "20.00",
			"billCurrency": "SOS",
			"billNumber": "26071801",
			"dueDate": "2026-07-18T11:45:00.001",
			"status": "PENDING",
			"partialPayAllowed": "0",
			"description": "Vehicle tax for plate GAR-1234 - Entry"
		}
	],
	"PayInfo": null
}
```

## Error Responses (Examples)

```json
{
	"requestId": "EzQMJVjN",
	"schemaVersion": "1.0",
	"responseHeader": {
		"timestamp": "20260718114500001",
		"resultCode": "1",
		"resultMessage": "Provide invoiceNumber, or provide both plateNumber and movement."
	},
	"billInfo": [],
	"PayInfo": null
}
```

```json
{
	"requestId": "EzQMJVjN",
	"schemaVersion": "1.0",
	"responseHeader": {
		"timestamp": "20260718114500001",
		"resultCode": "1",
		"resultMessage": "Invoice not found."
	},
	"billInfo": [],
	"PayInfo": null
}
```

# 🔐 Lightning SPP License System - Documentation

## Overview

Lightning SPP includes a professional licensing system supporting 4 tiers:

- **FREE**: 1,000 operations/month, 1 device
- **PAY-AS-YOU-GO**: €0.001/operation, min €9.99/month, 5 devices
- **UNLIMITED**: €99.99/year, unlimited operations, 10 devices ⭐ RECOMMENDED
- **ENTERPRISE**: Custom pricing, unlimited devices and operations

## License Key Format

```
LSPP-{TIER}-{SEG1}-{SEG2}-{SEG3}-{CHECKSUM}
```

**Example:**
```
LSPP-UNLTD-3F4D-6235-618C-7E23
```

Components:
- `LSPP`: Lightning SPP identifier
- `TIER`: FREE, PAYGO, UNLTD, ENTER
- `SEG1-3`: Random hex segments (cryptographically secure)
- `CHECKSUM`: SHA256-based validation

## CLI Commands

### Generate License (Development/Testing)

```bash
# Generate FREE tier license
wwwmmm generate-license free user@example.com

# Generate UNLIMITED tier license
wwwmmm generate-license unlimited clisonix@pm.me "ABA GmbH"

# Generate ENTERPRISE tier license
wwwmmm generate-license enterprise admin@company.com "Acme Corp"
```

### Activate License

```bash
wwwmmm activate LSPP-UNLTD-3F4D-6235-618C-7E23
```

### Check License Status

```bash
wwwmmm license
# or
wwwmmm status
```

### Deactivate License

```bash
wwwmmm deactivate
```

## Integration Example

```csharp
using LightningSPP.Licensing;

// Check if valid license exists
if (!LicenseManager.HasValidLicense())
{
    Console.WriteLine("No valid license. Running in FREE tier.");
}

// Get active license tier
var tier = LicenseManager.GetLicenseTier();
Console.WriteLine($"Active tier: {tier}");

// Check feature availability
if (LicenseManager.IsFeatureAvailable("ai"))
{
    // Use AI features
}

// Track usage (automatic limit checking)
if (LicenseManager.CheckAndIncrementUsage())
{
    // Perform operation
}
else
{
    Console.WriteLine("Operation limit reached.");
}

// Get usage statistics
var usage = LicenseManager.GetUsageThisMonth();
Console.WriteLine($"Used {usage} operations this month");
```

## License Storage

Licenses are stored in:
```
%APPDATA%\LightningSPP\license.json
```

**Windows Example:**
```
C:\Users\Admin\AppData\Roaming\LightningSPP\license.json
```

**Linux/Mac Example:**
```
~/.config/LightningSPP/license.json
```

## License JSON Structure

```json
{
  "Key": "LSPP-UNLTD-3F4D-6235-618C-7E23",
  "Tier": 2,
  "CustomerEmail": "clisonix@pm.me",
  "CompanyName": "ABA GmbH",
  "IssueDate": "2026-05-14T09:18:53Z",
  "ExpirationDate": "2027-05-14T09:18:53Z",
  "MaxDevices": 10,
  "MaxOperationsPerMonth": -1,
  "IsActive": true,
  "Features": "All features, Unlimited operations, €99.99/year..."
}
```

## Tier Limits

| Tier | Max Operations | Max Devices | AI Features | Premium Features |
|------|----------------|-------------|-------------|------------------|
| FREE | 1,000/month | 1 | ❌ | ❌ |
| PAY-AS-YOU-GO | Pay per use | 5 | ✅ | ✅ |
| UNLIMITED | Unlimited | 10 | ✅ | ✅ |
| ENTERPRISE | Unlimited | Unlimited | ✅ | ✅ |

## Features by Tier

### FREE Tier
- Basic scanning (TideWave)
- Standard processing (CLX)
- Draft printing
- 1,000 operations/month
- 1 device
- Community support

### PAY-AS-YOU-GO
- All scanning modes (TideWave, Resonance, NanoDecibel)
- All processing engines (CLX, CLX.i, XCL, Clisonix AI)
- All print qualities (Draft, Normal, High, Stigma)
- Pay €0.001/operation
- Minimum €9.99/month
- 5 devices
- Email support

### UNLIMITED (Recommended)
- Everything in PAY-AS-YOU-GO
- **Unlimited operations**
- **€99.99/year flat rate**
- 10 devices
- Priority email support
- Best value for regular users

### ENTERPRISE
- Everything in UNLIMITED
- Unlimited devices
- Custom SLA
- Dedicated support channel
- White-label options
- Custom integrations
- On-premise deployment options
- Volume discounts (15%-35% for 6+ locations)

## Purchase Licenses

**Production licenses can be purchased at:**

🛒 https://aba-gmbh.de/lightning-spp

Contact: clisonix@pm.me

## Security

- License keys are cryptographically signed with SHA256
- No reverse engineering possible from key alone
- Server-side validation (production)
- Secure storage with file permissions
- AOT compilation prevents reflection attacks

## Production Deployment

In production environments, license validation includes:

1. **Server-Side Verification**: Keys validated against licensing server
2. **Device Fingerprinting**: Prevent unauthorized key sharing
3. **Usage Telemetry**: Track actual operations for billing
4. **Automatic Renewal**: Email notifications before expiration
5. **Compliance Logging**: Audit trail for enterprise customers

## Development/Testing

For development and testing, use the `generate-license` command to create sample licenses locally. These are fully functional but should not be used in production.

**Production licenses must be purchased through official channels.**

## Support

For licensing issues:

📧 **Email**: clisonix@pm.me
🌐 **Web**: https://aba-gmbh.de
🏢 **Company**: ABA GmbH, Wattenscheider Hellweg 199, 44869 Bochum, Germany

## License Agreement

By activating a Lightning SPP license, you agree to:

- **MIT License** for software usage
- **Terms of Service** as published on aba-gmbh.de
- **Privacy Policy** (GDPR compliant)
- **Acceptable Use Policy**
- **Data Processing Agreement** (HIPAA compliant for healthcare)

See LICENSE, PRIVACY.md, COMPLIANCE.md for full details.

---

© 2025-2026 ABA GmbH. All rights reserved.

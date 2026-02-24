# Test Users

The following users were seeded in the Render PostgreSQL database (`backend_exam`) for testing purposes:

| ID | Name | Email | Password | Role |
| :--- | :--- | :--- | :--- | :--- |
| **1** | Admin Manager | `manager@example.com` | `manager123` | **MANAGER** |
| **2** | Support Tech | `support@example.com` | `pass` | **SUPPORT** |
| **3** | Regular User | `user@example.com` | `pass` | **USER** |

## How to use

You can obtain a JWT token for any of these users by hitting the login endpoint:

```bash
curl -X POST http://localhost:5050/auth/login \
     -H "Content-Type: application/json" \
     -d '{"email":"manager@example.com","password":"manager123"}'
```

Then pass the resulting token in the `Authorization: Bearer <token>` header for subsequent requests.

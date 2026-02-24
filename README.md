# Support Ticket Management API

This is my backend project for the helpdesk ticket system assignment. Built using ASP.NET Core 8 with PostgreSQL database and JWT for authentication.

## What I Used

- ASP.NET Core 8 Web API
- Entity Framework Core 8 
- PostgreSQL (hosted on Render)
- JWT Bearer tokens for auth
- BCrypt for password hashing
- Swagger UI for testing the API

## What You Need

Make sure you have these installed:
- .NET 8 SDK - download from microsoft website
- PostgreSQL (or use online database like I did)
- dotnet-ef tools for migrations: `dotnet tool install --global dotnet-ef`

## How to Run This Project

**Step 1:** Navigate to the project folder
```bash
cd SupportTicketAPI
```

**Step 2:** Update database connection

Open `appsettings.json` and change the connection string to your database. Mine looks like this but you need to use your own:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=support_tickets;Username=postgres;Password=yourpassword"
}
```

**Step 3:** Run migrations to create tables

```bash
dotnet ef database update
```

This creates all the tables and adds the 3 roles (MANAGER, SUPPORT, USER) automatically.

**Step 4:** Create a manager account

You need at least one MANAGER user to start. I just inserted it directly into the database:

```sql
INSERT INTO users (name, email, password, role_id, created_at)
VALUES (
  'Admin Manager',
  'manager@example.com',
  '$2a$11$yourBcryptHashHere',  -- use bcrypt hashed password here!
  1,                            -- 1 = MANAGER role
  NOW()
);
```

**NOTE:** Don't use plain text password! You can generate bcrypt hash online or use this if you have dotnet script:
```bash
dotnet script -e "Console.WriteLine(BCrypt.Net.BCrypt.HashPassword(\"yourpassword\"));"
```

**Step 5:** Start the server

```bash
dotnet run
```

The API will start on `http://localhost:5175` (check your terminal for the actual port).

Open Swagger UI at: `http://localhost:5175/docs`

## How to Test the API

1. Go to `/docs` in your browser
2. Login with your manager account using POST /auth/login
3. Copy the token you get back
4. Click the "Authorize" button at the top and paste your token
5. Now you can test all endpoints!

Create users with POST /users, create tickets with POST /tickets, etc.

## User Roles & Permissions

**MANAGER** - Can do everything
- Create tickets ✓
- See all tickets ✓
- Assign tickets to support ✓
- Change ticket status ✓
- Delete tickets ✓

**SUPPORT** - Handles assigned tickets
- Create tickets ✗
- See only assigned tickets ✓
- Assign tickets ✓
- Change status ✓
- Delete tickets ✗

**USER** - Regular employees
- Create tickets ✓
- See own tickets only ✓
- Cannot assign ✗
- Cannot change status ✗
- Cannot delete ✗

## Important: Ticket Status Flow

Tickets MUST follow this exact order, you can't skip steps:

```
OPEN → IN_PROGRESS → RESOLVED → CLOSED
```

If you try to skip (like OPEN → RESOLVED) or go backwards, you'll get a 400 error.

## API Response Codes

- 200 = Success
- 201 = Created something new
- 204 = Deleted successfully (no response body)
- 400 = Bad request or validation failed
- 401 = Not logged in or invalid token
- 403 = You don't have permission
- 404 = Not found

## Project Structure

```
Controllers/
  - AuthController.cs (login)
  - UsersController.cs (create/list users)
  - TicketsController.cs (ticket CRUD + assign + status)
  - CommentsController.cs (add/edit/delete comments)

Models/
  - User, Role, Ticket, TicketComment, TicketStatusLog

DTOs/
  - All the request/response data transfer objects

Data/
  - AppDbContext.cs (EF Core setup)
```

## Notes

- All passwords are hashed with bcrypt, never stored as plain text
- JWT tokens expire after 8 hours
- Status changes are logged in ticket_status_logs table
- Deleting a ticket cascades to delete its comments and logs
- You can't assign tickets to users with USER role (only MANAGER/SUPPORT)

## Endpoints

**Auth:**
- `POST /auth/login` - Login and get JWT token

**Users (MANAGER only):**
- `POST /users` - Create new user
- `GET /users` - List all users

**Tickets:**
- `POST /tickets` - Create ticket (USER, MANAGER)
- `GET /tickets` - Get tickets (filtered by role)
- `PATCH /tickets/{id}/assign` - Assign to support (MANAGER, SUPPORT)
- `PATCH /tickets/{id}/status` - Update status (MANAGER, SUPPORT)
- `DELETE /tickets/{id}` - Delete ticket (MANAGER only)

**Comments:**
- `POST /tickets/{id}/comments` - Add comment
- `GET /tickets/{id}/comments` - Get all comments
- `PATCH /comments/{id}` - Edit comment (author or MANAGER)
- `DELETE /comments/{id}` - Delete comment (author or MANAGER)

## Validation Rules

- Ticket title must be at least 5 characters
- Ticket description must be at least 10 characters
- Priority must be LOW, MEDIUM, or HIGH
- Status must follow the linear progression (can't skip)
- Email format must be valid

## Database Schema

The database has 5 tables:
1. **roles** - MANAGER, SUPPORT, USER
2. **users** - User accounts linked to roles
3. **tickets** - Support tickets with status and priority
4. **ticket_comments** - Comments on tickets
5. **ticket_status_logs** - Logs every status change

Relationships are set up so deleting a ticket automatically deletes its comments and logs (cascade delete).

# Database Schema

Hummingbird API uses PostgreSQL with Entity Framework Core 8 and two separate database contexts: **AppDbContext** (per-tenant) and **MasterDbContext** (shared tenant registry).

**Migrations applied:** `20260406131415_InitialCreate`

---

## AppDbContext

### Positions

Job roles with shift and salary defaults.

| Column | Type | Nullable | Notes |
|--------|------|----------|-------|
| Id | integer | NO | PK, identity |
| Name | text | NO | Job title |
| DefaultSalary | numeric(18,2) | NO | |
| ShiftType | text | NO | Enum: `Morning`, `Afternoon`, `Evening` |
| ClockInTime | interval | NO | Expected start time |
| ClockOutTime | interval | NO | Expected end time |
| TotalHours | numeric(5,2) | NO | Expected daily hours |
| ServiceChargePercentage | numeric(5,2) | NO | SC allocation for Version A |

**Indexes:** `PK_Positions`

**Seed data:**

| Name | Salary | Shift | Hours | SC% |
|------|--------|-------|-------|-----|
| Food and Beverage | 15,000 | Morning 08:00–17:00 | 9 | 25 |
| Cleaner | 12,000 | Morning 07:00–16:00 | 9 | 20 |
| Receptionist | 18,000 | Morning 08:00–17:00 | 9 | 30 |
| Manager | 35,000 | Morning 08:00–17:00 | 9 | 25 |

---

### Employees

| Column | Type | Nullable | Default | Notes |
|--------|------|----------|---------|-------|
| Id | integer | NO | identity | PK |
| EmployeeCode | text | NO | | Unique identifier (from CSV import) |
| Name | text | NO | | First name |
| Surname | text | NO | | Last name |
| PositionId | integer | NO | | FK → Positions.Id |
| Salary | numeric(18,2) | NO | | May override position default |
| DateJoined | timestamptz | NO | | Start date (UTC) |
| Status | text | NO | `Active` | Enum: `Active`, `Terminated` |

**Indexes:**
- `PK_Employees`
- `IX_Employees_EmployeeCode` (UNIQUE)
- `IX_Employees_PositionId`

**FK:** `FK_Employees_Positions_PositionId` — on delete **RESTRICT**

---

### TimeAttendanceRecords

Daily clock-in/out records. Denormalized — no FK to Employees; linked via `EmployeeCode`.

| Column | Type | Nullable | Default | Notes |
|--------|------|----------|---------|-------|
| Id | integer | NO | identity | PK |
| EmployeeCode | text | NO | | Matches Employees.EmployeeCode (loose coupling) |
| EmployeeName | text | NO | | Denormalized |
| Department | text | NO | | Denormalized |
| Date | timestamptz | NO | | Attendance date (UTC) |
| ShiftType | text | NO | | |
| ClockIn | interval | YES | NULL | Actual clock-in time |
| ClockOut | interval | YES | NULL | Actual clock-out time |
| TotalHours | numeric(5,2) | NO | | Calculated hours worked |
| IsMissing | boolean | NO | false | No-show indicator |
| IsPenalty | boolean | NO | false | Late arrival (>30 min) |
| Month | integer | NO | | 1–12 |
| Year | integer | NO | | |

**Indexes:**
- `PK_TimeAttendanceRecords`
- `IX_TimeAttendanceRecords_EmployeeCode_Date` (UNIQUE)

---

### PayrollRecords

Monthly payroll calculations per employee.

| Column | Type | Nullable | Default | Notes |
|--------|------|----------|---------|-------|
| Id | integer | NO | identity | PK |
| EmployeeId | integer | NO | | FK → Employees.Id |
| Month | integer | NO | | 1–12 |
| Year | integer | NO | | |
| BaseSalary | numeric(18,2) | NO | | |
| Deductions | numeric(18,2) | NO | | Late + missing day penalties |
| ServiceChargeBonus | numeric(18,2) | NO | | SC distribution amount |
| NetSalary | numeric(18,2) | NO | | BaseSalary − Deductions + ServiceChargeBonus |
| TotalScheduledDays | integer | NO | | Expected working days |
| WorkDays | integer | NO | | Actual days worked |
| PenaltyDays | integer | NO | | Days with late arrival |
| MissingDays | integer | NO | | No-show days |
| ServiceChargeInput | decimal | NO | | Total SC revenue for the month |
| ServiceChargeVersion | text | NO | `"A"` | `"A"` = fixed rate, `"B"` = workday rate |
| CalculatedAt | timestamptz | NO | UtcNow | When record was computed |

**Indexes:**
- `PK_PayrollRecords`
- `IX_PayrollRecords_EmployeeId_Month_Year` (UNIQUE)

**FK:** `FK_PayrollRecords_Employees_EmployeeId` — on delete **CASCADE**

---

### AppConfigs

Key-value configuration store.

| Column | Type | Nullable | Default | Notes |
|--------|------|----------|---------|-------|
| Id | integer | NO | identity | PK |
| Key | text | NO | | Config key |
| Value | text | NO | | Config value |
| Description | text | NO | | Human-readable description |
| UpdatedAt | timestamptz | NO | UtcNow | Last update time |

**Indexes:**
- `PK_AppConfigs`
- `IX_AppConfigs_Key` (UNIQUE)

**Seed data:**

| Key | Default Value | Description |
|-----|---------------|-------------|
| ServiceChargeVersion | `"A"` | A = fix-rate per employee, B = workday-rate |
| CompanyName | `"My Hotel"` | Company / property name |
| Onboarded | `"false"` | Whether initial setup has been completed |

---

## MasterDbContext

### Tenants

Tenant registry in the shared master database.

| Column | Type | Nullable | Default | Notes |
|--------|------|----------|---------|-------|
| Id | integer | NO | identity | PK |
| Name | text | NO | | Display name |
| Subdomain | text | NO | | URL subdomain (e.g. `tenant1`) |
| DatabaseName | text | NO | | PostgreSQL database name |
| ConnectionString | text | NO | | Database connection string |
| IsActive | boolean | NO | true | |
| CreatedAt | timestamptz | NO | UtcNow | |

**Indexes:**
- `PK_Tenants`
- `IX_Tenants_Subdomain` (UNIQUE)
- `IX_Tenants_DatabaseName` (UNIQUE)

---

## Relationships

```
Positions (1) ──[RESTRICT]──> Employees (N)
                                   │
                              [CASCADE]
                                   │
                                   └──> PayrollRecords (N)

TimeAttendanceRecords  (no FK — loose coupling via EmployeeCode)

AppConfigs  (standalone)

Tenants  (MasterDbContext — separate database)
```

---

## Enumerations

Both stored as strings in the database.

**EmployeeStatus** (`Models/Employee.cs`): `Active` | `Terminated`

**ShiftType** (`Models/Position.cs`): `Morning` | `Afternoon` | `Evening`

---

## Notes

- All `DateTime` columns use `timestamp with time zone` (UTC).
- Monetary amounts use `numeric(18,2)`; hours/percentages use `numeric(5,2)`.
- Identity strategy: PostgreSQL identity by default (sequences).
- Max identifier length: 63 characters.

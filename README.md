# PriceEngine (ASP.NET Core 9 + SQLite)

A small demo app: browse bus trips, calculate dynamic prices (occupancy, time-pressure, coupon), manage data in an admin panel, and run bulk price updates via a background queue. Includes structured logging with Serilog and correlation IDs.

---

## ⭐ Features

- **User UI (`wwwroot/index.html`)**
  - Filter by **origin / destination / day**, sort by **departure time** (asc/desc).
  - Enter **coupon codes**; clear in-page error messages for invalid/expired coupons.
  - Price breakdown tags (e.g., **Occupancy discount**, **Early booking**, **Coupon**).

- **Admin UI (`wwwroot/admin.html`)**
  - Add/delete buses and coupons.
  - Update **sold seats** with **auto-save (debounced)**.
  - **Bulk Price Update (Queued)**: run % price changes in the **background** and monitor progress/status live.

- **Backend**
  - ASP.NET Core Minimal APIs + Entity Framework Core (SQLite).
  - Pricing rules: `OccupancyRule`, `TimePressureRule`, `CouponRule`, coordinated by `PricingEngine`.
  - Logs to **Console** and rolling **File** via **Serilog**, enriched with a **CorrelationId** per request.

- **Data**
  - On first start, seeds **≥100** bus records across Turkish cities with varied prices/capacities/occupancy and future departures.
  - Two initial coupons: `EARLY10` (%10), `WELCOME5` (%5).

- **Security**
  - Cookie Authentication for admin-only APIs.
  - Login at `/login` (UI) → `/api/auth/login`.
  - Credentials: **admin / admin**.

---

## 🧰 Requirements (Install)

Only install these locally; NuGet packages are restored by the project:

- **.NET SDK 9.0.200**
- **EF CLI (global tool)**: `dotnet tool install -g dotnet-ef`

> Optional: **Docker 24+** if you prefer containerized runs.

---

## 📁 Project Structure


PriceEngine/
├─ PriceEngine.sln
├─ src/
│ └─ PriceEngine.Api/
│ ├─ Program.cs
│ ├─ appsettings.json
│ ├─ PriceEngine.Api.csproj
│ ├─ Data/
│ │ ├─ AppDbContext.cs
│ │ └─ DbInitializer.cs
│ ├─ Models/ # Bus, Coupon, PriceRequest/Response, Job, etc.
│ ├─ Pricing/ # OccupancyRule, TimePressureRule, CouponRule, PricingEngine
│ ├─ Services/ # EfCouponRepository, Queue, Background worker
│ ├─ wwwroot/
│ │ ├─ index.html
│ │ └─ admin.html
│ └─ Dockerfile
├─ docker-compose.yml
├─ requirements.txt
├─ README.md
└─ .dockerignore


---

## 🚀 Quick Start

> Commands below use **Windows CMD** (not PowerShell). On macOS/Linux, replace path separators accordingly.

```cmd
cd src\PriceEngine.Api
dotnet restore
dotnet build
dotnet run


Open:

User UI: http://localhost:5279/ (or the port printed in your console)

Admin: http://localhost:5279/admin (first visit redirects to /login)

Swagger: http://localhost:5279/swagger

---

**## ⚙️ Configuration **

appsettings.json:

{
  "Logging": {
    "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" }
  },
  "AllowedHosts": "*",

  "ConnectionStrings": {
    "Default": "Data Source=priceengine.db"
  },

  "PricingRules": {
    "Occupancy": {
      "LowThreshold": 20,
      "HighThreshold": 80,
      "LowDiscountPercent": 10,
      "HighIncreasePercent": 20
    },
    "TimePressure": {
      "IncreasePercent": 15,
      "DiscountPercent": 15,
      "HoursThreshold": 24,
      "DaysThreshold": 30
    }
  },

  "Serilog": {
  "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.File" ],
  "MinimumLevel": "Information",
  "WriteTo": [
    {
      "Name": "Console",
      "Args": {
        "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}"
      }
    },
    {
      "Name": "File",
      "Args": {
        "path": "logs/priceengine-.log",
        "rollingInterval": "Day",
        "retainedFileCountLimit": 7,
        "shared": true,
        "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties:j}{NewLine}{Exception}"
      }
    }
  ],
  "Enrich": [ "FromLogContext" ]
}

}



---

api_overview:
  public:
    - name: ListBuses
      method: GET
      path: /buses
      auth: none
      description: Returns all buses ordered by departure time.
      responses:
        "200":
          description: OK
          schema: Bus[]
    - name: CalculatePrice
      method: POST
      path: /price/calculate
      auth: none
      description: Calculates final price using pricing rules (occupancy, time pressure, coupon).
      request:
        contentType: application/json
        body:
          type: object
          required: [basePrice, capacity, soldSeats, departureTime]
          properties:
            basePrice: { type: number, format: decimal, example: 1000 }
            capacity: { type: integer, example: 50 }
            soldSeats: { type: integer, example: 20 }
            departureTime: { type: string, format: date-time, example: "2025-11-20T09:00:00Z" }
            couponCode: { type: string, nullable: true, example: "EARLY10" }
      responses:
        "200":
          description: OK
          schema: PriceResponse
        "400":
          description: Bad Request (validation or expired coupon)
          example: { message: "Kuponun süresi dolmuş." }
        "404":
          description: Coupon not found
          example: { message: "Kupon bulunamadı." }
    - name: GetCoupon
      method: GET
      path: /coupon/{code}
      auth: none
      description: Returns coupon info or an error if not found/expired.
      params:
        - name: code
          in: path
          required: true
          type: string
      responses:
        "200": { description: OK, schema: Coupon }
        "400": { description: Expired, example: { message: "Kuponun süresi dolmuş." } }
        "404": { description: Not Found, example: { message: "Kupon bulunamadı." } }

  admin:
    security: CookieAuth (username: admin, password: admin)
    endpoints:
      - { method: GET,  path: /api/buses,  description: List buses (admin) }
      - { method: POST, path: /api/buses,  description: Create bus, requestBody: CreateBusRequest, responses: { "201": Bus } }
      - { method: PUT,  path: /api/buses/{id}/sold, description: Update sold seats, requestBody: UpdateSoldSeatsRequest, responses: { "200": Bus } }
      - { method: DELETE, path: /api/buses/{id}, description: Delete bus, responses: { "204": No Content } }
      - { method: GET,  path: /api/coupons, description: List coupons }
      - { method: POST, path: /api/coupons, description: Create coupon, requestBody: CreateCouponRequest, responses: { "201": Coupon, "409": { message: "Bu kupon kodu zaten var." } } }
      - { method: DELETE, path: /api/coupons/{id}, description: Delete coupon, responses: { "204": No Content } }
      - method: POST
        path: /api/jobs/bulk-price
        description: Enqueue bulk base price update (+/- percent).
        request:
          contentType: application/json
          body: { type: object, required: [percent], properties: { percent: { type: number, example: -10 } } }
        responses:
          "202": { description: Accepted, schema: { id: string, status: JobStatus } }
      - method: GET
        path: /api/jobs/{id}
        description: Get job status/progress.
        responses:
          "200": { description: OK, schema: BackgroundJob }
      - method: POST
        path: /api/admin/generate-buses
        query: { reset: { type: boolean, default: false } }
        description: Ensure at least 100 seeded buses; if reset=true, recreate.
        responses:
          "200": { description: OK, schema: { count: integer } }

schemas:
  Bus:
    type: object
    properties:
      id: { type: integer }
      name: { type: string, example: "İstanbul → Ankara" }
      capacity: { type: integer }
      soldSeats: { type: integer }
      basePrice: { type: number, format: decimal }
      departureTime: { type: string, format: date-time }
  Coupon:
    type: object
    properties:
      id: { type: integer }
      code: { type: string, example: "EARLY10" }
      percent: { type: integer, example: 10 }
      expireAt: { type: string, format: date-time }
  CreateBusRequest:
    type: object
    required: [name, capacity, basePrice, departureTime]
    properties:
      name: { type: string }
      capacity: { type: integer, minimum: 1 }
      soldSeats: { type: integer, minimum: 0 }
      basePrice: { type: number, format: decimal, minimum: 0.01 }
      departureTime: { type: string, format: date-time }
  UpdateSoldSeatsRequest:
    type: object
    required: [soldSeats]
    properties:
      soldSeats: { type: integer, minimum: 0 }
  CreateCouponRequest:
    type: object
    required: [code, percent, expireAt]
    properties:
      code: { type: string }
      percent: { type: integer, minimum: 1, maximum: 100 }
      expireAt: { type: string, format: date-time }
  PriceResponse:
    type: object
    properties:
      finalPrice: { type: number, format: decimal }
      steps:
        type: array
        items:
          type: object
          properties:
            rule: { type: string, enum: [OccupancyRule, TimePressureRule, CouponRule] }
            before: { type: number, format: decimal }
            delta:  { type: number, format: decimal, description: "Negative for discounts" }
            after:  { type: number, format: decimal }
  BackgroundJob:
    type: object
    properties:
      id: { type: string, format: uuid }
      type: { type: string, example: "BulkPriceUpdate" }
      status: { $ref: "#/schemas/JobStatus" }
      total: { type: integer, nullable: true }
      processed: { type: integer, nullable: true }
      error: { type: string, nullable: true }
      enqueuedAt: { type: string, format: date-time }
      startedAt: { type: string, format: date-time, nullable: true }
      finishedAt: { type: string, format: date-time, nullable: true }
  JobStatus:
    type: string
    enum: [Queued, Running, Succeeded, Failed]

background_jobs:
  ui_card: "Toplu Fiyat Güncelle (Kuyruklu)"
  flow:
    - step: "POST /api/jobs/bulk-price"
      result: "202 Accepted with { id, status }"
    - step: "GET /api/jobs/{id}"
      result: "Poll for status/progress (Queued/Running/Succeeded/Failed)"
  implementation:
    queue: "In-memory Channel (bounded)"
    worker: "BackgroundService"
    note: "Can be swapped for RabbitMQ/Redis for distributed processing."

logging_and_correlation:
  sinks:
    - Serilog.Console
    - Serilog.File (path: logs/priceengine-.log, rolling: Daily)
  correlation:
    header: "X-Correlation-Id"
    behavior: "Generated if missing; pushed to Serilog LogContext for every request."
  rules_logging:
    uses: "ILogger<T>"
    pattern: "Before / Delta / After fields per rule application"

docker:
  compose:
    commands:
      - "mkdir data logs"
      - "docker compose build"
      - "docker compose up -d"
    app_url: "http://localhost:5279"
    volumes:
      - "./data:/data   # SQLite persistence"
      - "./logs:/logs   # Serilog files"
  single_run:
    build: "docker build -f src/PriceEngine.Api/Dockerfile -t priceengine:local ."
    run:
      image: "priceengine:local"
      name: "priceengine"
      ports: ["5279:8080"]
      volumes:
        - "%cd%\\data:/data"
        - "%cd%\\logs:/logs"
      env:
        ASPNETCORE_ENVIRONMENT: "Production"
        ConnectionStrings__Default: "Data Source=/data/app.db"
        Serilog__WriteTo__1__Args__path: "/logs/priceengine-.log"

development_tips:
  migrations_optional: true
  commands:
    migrations:
      - "cd src\\PriceEngine.Api"
      - "dotnet ef migrations add <Name>"
      - "dotnet ef database update"
    update_ef_tool:
      - "dotnet tool update -g dotnet-ef"



🪵 Logging & Correlation

    Serilog sinks: Console + rolling File (logs/priceengine-.log).

    Middleware attaches X-Correlation-Id to each request and log context.

    Rules and engine use ILogger<T> with structured fields: Before / Delta / After.
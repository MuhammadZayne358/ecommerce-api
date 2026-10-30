# AuraScents API

REST API for **AuraScents**, a full-stack perfume e-commerce application. Built with ASP.NET Core 8, Entity Framework Core and PostgreSQL.

**Live demo:** https://ecommerce-client-orpin-kappa.vercel.app
**Frontend repo:** https://github.com/MuhammadZayne358/ecommerce-client

## Features

**Customers**
- Register, login and logout with JWT authentication (token stored in an HttpOnly cookie)
- Forgot password / reset password through an emailed link
- Browse products with pagination, categories and product details
- Product reviews and average ratings
- Shopping cart
- Checkout with Cash on Delivery or Card (demo only, no real payment gateway)
- Address book: save up to 5 shipping addresses, choose a default, delete addresses
- Order history and order details
- Contact form and newsletter subscription

**Admin** (role-based authorization)
- Dashboard with store statistics
- Create, edit and delete products (with image upload) and categories
- View and manage customer orders
- Read and manage contact messages

## Tech stack

| Area | Technology |
| --- | --- |
| Framework | ASP.NET Core 8 Web API |
| Language | C# |
| ORM | Entity Framework Core 8 (Code First migrations) |
| Database | PostgreSQL (Npgsql), hosted on Neon |
| Auth | JWT Bearer, HttpOnly cookie, role-based authorization |
| Docs | Swagger / OpenAPI (Swashbuckle) |
| Email | SMTP email service for password reset |

## Project structure

```
ECommerce.API/
├── Controllers/   # API endpoints
├── Entities/      # Database models (User, Product, Order, UserAddress, ...)
├── DTOs/          # Request and response models
├── Data/          # ApplicationDbContext and relationships
├── Mappings/      # Entity to DTO mapping
├── Middleware/    # Custom middleware
├── Services/      # Email service
├── Migrations/    # EF Core migrations
└── wwwroot/       # Uploaded product images
```

## API overview

| Route | Purpose |
| --- | --- |
| `/api/Auth` | Register, login, logout, profile, password reset |
| `/api/Products` | Product listing, details, admin create / update / delete |
| `/api/Categories` | Category listing, admin create / update / delete |
| `/api/Cart` | Cart items for the logged-in user |
| `/api/Orders` | Checkout, order history, order details |
| `/api/Addresses` | Saved shipping addresses (list, add, set default, delete) |
| `/api/Reviews` | Product reviews |
| `/api/Contact` | Contact messages |
| `/api/Newsletter` | Newsletter subscription |
| `/api/AdminDashboard`, `/api/AdminOrders` | Admin-only statistics and order management |

Full details are available in Swagger (`/swagger`) when running in Development.

## Getting started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A PostgreSQL database (local or a free one from [Neon](https://neon.tech))

### Run locally

```bash
git clone https://github.com/MuhammadZayne358/ecommerce-api.git
cd ecommerce-api

# Store secrets outside the repo
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=aurascents;Username=postgres;Password=YOUR_PASSWORD"
dotnet user-secrets set "Jwt:Key" "a-long-random-secret-of-at-least-32-characters"

# Create the database tables
dotnet ef database update

# Start the API
dotnet run
```

Swagger opens at the URL printed in the console, followed by `/swagger`.

The frontend runs on `http://localhost:5174`, which is the origin allowed by the CORS policy in `Program.cs`.

### Creating an admin user
Register a normal account from the app, then change that user's `Role` to `Admin` directly in the database.

## Deployment

- **API:** Somee (IIS, ASP.NET Core in-process hosting)
- **Database:** Neon (PostgreSQL)
- **Frontend:** Vercel. It forwards `/api` and `/images` requests to this API using rewrites, so the browser only talks to one domain and the login cookie works on all browsers.

## Notes

- The hosting is on free plans, so the first request after a period of inactivity can be slow.
- Images uploaded through the admin panel are stored on the server disk, which free hosting does not guarantee to keep.
- The card payment option is a demo and no real payments are processed.

## Author

**Muhammad Zain**, .NET developer
GitHub: [@MuhammadZayne358](https://github.com/MuhammadZayne358)

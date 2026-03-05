# WhiskeyTracker 🥃

**WhiskeyTracker** is a personal inventory and tasting journal application built with ASP.NET Core Razor Pages. It allows whiskey enthusiasts to catalog their collection, track bottle status (Full, Opened, Empty), and record tasting notes through guided sessions.

## 🚀 Features

* **Library Management:** detailed tracking of whiskey expressions including Distillery, Region, Age, ABV, and Cask Type.
* **Inventory Control:** Track individual bottles, purchase prices, locations, and open/empty status.
* **Tasting Wizard:** A guided interface for live tasting sessions, allowing you to rate and note whiskies flight-style.
* **Dashboard:** High-level view of your collection stats and recent activity.
* **Search & Filter:** Quickly find bottles by text or region.
* **Image Support:** Upload and display bottle images.

## 🛠 Tech Stack

* **Framework:** ASP.NET Core 9.0 (Razor Pages)
* **Language:** C#
* **Database:** PostgreSQL
* **ORM:** Entity Framework Core
* **Frontend:** Bootstrap 5, jQuery Validation

## ⚙️ Local Development Setup

Follow these instructions to get the project running on your local machine.

### Prerequisites

1.  [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
2.  [PostgreSQL](https://www.postgresql.org/download/) (Local install or Docker container)
3.  Visual Studio, VS Code, or JetBrains Rider.

### Installation Steps

1.  **Clone the repository:**
    ```bash
    git clone [https://github.com/yourusername/whiskeytracker.git](https://github.com/yourusername/whiskeytracker.git)
    cd whiskeytracker
    ```

2.  **Configure Database:**
    Ensure your `appsettings.json` (or `appsettings.Development.json`) contains a valid connection string to your local PostgreSQL instance.

    ```json
    "ConnectionStrings": {
      "DefaultConnection": "Host=localhost;Database=WhiskeyTracker;Username=postgres;Password=yourpassword"
    }
    ```

3.  **Apply Migrations:**
    Create the database schema using Entity Framework tools. Open your terminal in the project root:

    ```bash
    dotnet ef database update
    ```

4.  **Run the Application:**
    ```bash
    dotnet run
    ```
    Open your browser to `https://localhost:7001` (or the port specified in the console).

## 📂 Project Structure

* **`Data/`**: Contains the C# entity models (`Whiskey.cs`, `Bottle.cs`, `TastingSession.cs`) and the EF Context.
* **`Pages/`**:
    * **`Whiskies/`**: Standard CRUD pages for managing the library and inventory.
    * **`Tasting/`**: The customized "Wizard" flow for tasting sessions.
    * **`Shared/`**: Layouts and partial views.
* **`wwwroot/images/`**: Stores uploaded bottle images.

## 📝 Roadmap / Future Features

* **Multi-User / Live Tasting Sessions:** Allow multiple people to access, join, and comment on a particular tasting session concurrently. (From Discussion #63)
* **Granular Tasting Impressions & Tags:** Separate "Nose," "Palate," and "Finish" into individual input fields and introduce a tagging system for flavors. (From Discussion #62)
* **Infinity Bottle:** Logic to track a living blend of whiskies.
* **Stats Engine:** Advanced analytics (Cost per dram, favorite regions).
* **Data Export:** Export collection to CSV/Excel.
* **Global Ratings:** Refactor static whiskey ratings to be an average of all tasting session scores.
* **Automated Unit Testing:** Add a test suite (xUnit or NUnit) to verify business logic and prevent regressions.
* **Social Features:** Allow users to share their collections and tasting notes with friends.
* **Admin Dashboard:** Add an admin dashboard to manage users and collections.
* **Mobile App:** Add a mobile app to access the collection.
* **API:** Add an API to access the collection.
* **Clean Build Warnings:** Fix all build warnings.

## 🤝 Contributing

1.  Fork the project.
2.  Create your feature branch (`git checkout -b feature/AmazingFeature`).
3.  Commit your changes (`git commit -m 'Add some AmazingFeature'`).
4.  Push to the branch (`git push origin feature/AmazingFeature`).
5.  Open a Pull Request.

---
*Sláinte!* 🥃
*Cheers!* 🥃
**Whiskey Tracker Web Application \- Product Requirements Document**

**I. Overview & Goals**

* **Purpose:** This document outlines the requirements for the "Whiskey Tracker" web application, a tool designed to allow users to log and track information about the different whiskies they have tasted.  
* **Scope:** This PRD covers the core features and functionalities of the initial release of the web application. Future enhancements may be addressed in subsequent versions.  
* **Platform:** The initial release will be a web-only application, with architectural decisions made to support a potential future transition to a Progressive Web App (PWA).
* **Target Audience (of this document):** This document is intended for the development team, designers, stakeholders, and anyone involved in the creation of the Whiskey Tracker application.  
* **Version:** 1.0  
* **Business Goals:**  
  * Provide a useful and engaging tool for whiskey enthusiasts.  
  * Potentially explore monetization options in the future (e.g., premium features, ad-supported content – though not in the initial scope).  
  * Build a community of whiskey lovers (longer-term vision).  
* **Product Goals:**  
  * Allow users to easily record details about whiskies they have tried.  
  * Enable users to organize and categorize their whiskey experiences.  
  * Provide insights and summaries of the user's tasting history.  
  * Offer a user-friendly and intuitive interface.  
* **Key Performance Indicators (KPIs):**  
  * Number of registered users.  
  * Number of whiskies logged.  
  * Frequency of user engagement (e.g., daily/weekly active users).  
  * User satisfaction (measured through feedback or surveys).

**II. Target Audience & User Needs**

* **User Personas:**  
  * **The Enthusiast:** Passionate about whiskey, actively seeking new expressions, wants to keep detailed records of their tastings, may share their notes with others.  
  * **The Explorer:** Relatively new to whiskey, wants to learn more about different types, uses the app to remember what they like and dislike.  
  * **The Collector:** Focuses on acquiring and potentially tasting rare or unique bottles, wants to track their collection and tasting experiences.  
* **User Needs and Pain Points:**  
  * Difficulty remembering details about previously tasted whiskies (e.g., tasting notes, purchase date, price).  
  * Lack of a centralized place to store and organize their whiskey experiences.  
  * Desire to easily compare and contrast different whiskies.  
  * Potential interest in sharing their experiences or learning from others.  
  * Need to track the contents and evolution of their infinity bottles.

**III. Product Description & User Stories**

* **High-Level Overview:** The Whiskey Tracker is a web application that allows users to log and manage information about the whiskies they have tasted. Users can record details such as the distillery, region, age, tasting notes, rating, purchase information, and more. The application will provide tools for organizing, searching, and summarizing this data.  
* **Key Features:**  
  * Whiskey Logging: Ability to add new whiskies with various attributes, including a bottle image.  
  * Tasting Notes: Rich text editor for recording sensory experiences.  
  * Rating System: Standardized way to rate whiskies.  
  * Organization: Ability to categorize and tag whiskies.  
  * Search and Filtering: Tools to easily find specific entries.  
  * Personalized Statistics: Summaries of the user's tasting history.  
  * Inventory Tracking: Ability to manage and track the user's current whiskey collection.  
  * Tasting Sessions: Functionality to log and record details of specific tasting events.  
  * Infinity Bottle Tracking: Ability to create and manage infinity bottles, tracking their contents.  
* **User Stories:**  
  * As a user, I want to be able to easily add a new whiskey to my log with details like distillery, name, and region, **and upload a picture of the bottle,** so that I can keep a record of what I've tried.  
  * As a user, I want to be able to record my tasting notes for a whiskey, including aroma, palate, and finish, so that I can remember my sensory experience.  
  * As a user, I want to be able to rate a whiskey on a scale of 1-5 stars so that I can easily see my overall impression.  
  * As a user, I want to be able to search my logged whiskies by distillery or region so that I can quickly find specific entries.  
  * As a user, I want to see a summary of the types of whiskies I've tasted (e.g., by region or style) so that I can understand my preferences better.  
  * As a user, I want to be able to add whiskies to my inventory, indicating the quantity I own, so that I can keep track of my collection.  
  * As a user, when I log a tasting, I want to be able to associate it with a specific whiskey from my inventory.  
  * As a user, I want to be able to create a "tasting session" and record notes specific to that session, independent of the general tasting notes for a particular whiskey.  
  * As a user, within a tasting session, I want to be able to record the order in which the whiskies were tasted.  
  * As a user, I want to be able to view a dedicated page for each logged whiskey, **including the bottle image.**  
  * As a user I want to see all general tasting notes recorded for a whiskey on the whiskey detail page.  
  * As a user, I want to see a list of all tasting sessions in which that whiskey was included, with the session-specific notes and the order it was tasted in on the whiskey detail page.  
  * As a user, I want to be able to create an infinity bottle and add whiskies from my inventory to it.  
  * As a user, I want to be able to record the date and amount of each whiskey added to my infinity bottle.  
  * As a user, I want to be able to see a list of all the whiskies in my infinity bottle, including the date and amount of each addition.  
  * As a user, I want to be able to add general tasting notes to my infinity bottle as a whole.

**IV. Functional Requirements**

* **User Authentication:**  
  * Users should be able to create a new account.  
  * Users should be able to log in to their existing account.  
  * Users should be able to reset their password.  
  * The system should support two user roles:
    * Standard User: Regular users who can manage their own whiskey collections, tasting notes, and infinity bottles.
    * Administrator: Users with elevated privileges who can manage the application, including user accounts and system data.
  * Administrators should have access to administrative functions, including:
    * User account management
    * System configuration
    * Access to application analytics and usage statistics
* **Whiskey Database & Data Management:**
  * The system should include a pre-populated database of common whiskies, distilleries, and regions when possible (using available free APIs or datasets).
  * The system should allow administrators to manage and update the central whiskey database.
  * Users should be able to search this database when adding whiskey entries.
  * Manual search functionality will be the primary method for finding whiskies (automated image recognition is not required).
  * Users should be able to suggest additions or corrections to the central database, subject to administrator approval.
  * Users should always have the option to create fully custom whiskey entries if they can't find a match in the database.
  * Each whiskey in the central database should have a unique identifier and standard fields (distillery, region, type, etc.).
  * The whiskey categorization system should include:
    * **Countries of Origin**: Tracking whiskey-producing countries (Scotland, United States, Ireland, Japan, etc.)
    * **Regions**: Geographic regions within countries (e.g., Speyside, Highlands, Islay for Scotland; Kentucky, Tennessee for the US)
    * **Whiskey Categories**: Production methods and styles (Single Malt, Blended, Bourbon, Rye, etc.)
    * **Mashbill Tracking**: For applicable whiskeys, recording grain recipe compositions with percentages
    * **Production Attributes**: Tracking peated/non-peated, chill-filtered/non-chill-filtered, and colored/natural color
  * The system should come pre-populated with standard categorization data including:
    * Common whiskey-producing countries with descriptions
    * Standard regional classifications for major whiskey regions
    * Production style definitions with explanations
    * Common mashbill recipes (Traditional Bourbon, Wheated Bourbon, High-Rye, etc.)
  * Users should be able to add custom categorization entries if needed

* **Whiskey Entry:**  
  * Users should be able to add a new whiskey entry with fields for:  
    * Distillery  
    * Name/Expression  
    * Region  
    * Age Statement  
    * Bottling Date  
    * Cask Type  
    * ABV  
    * Purchase Date  
    * Purchase Price  
    * Where Purchased  
    * Bottle Status (e.g., Full, Opened, Empty)  
    * Image (optional file upload) \<-- Added image upload  
  * Users should be able to edit and delete their whiskey entries.  
* **Tasting Notes (General):**  
  * Each whiskey entry should have a rich text field for general tasting notes (aroma, palate, finish, overall thoughts) associated with that specific whiskey, regardless of the tasting session.  
* **Rating:**  
  * Users should be able to assign a 1-5 star rating to each whiskey.
  * The rating system should be consistent across all whiskey entries, tasting sessions, and reports.
  * Ratings should be displayed visually using star icons for intuitive understanding.
  * The system should allow partial or half-star ratings if desired in future updates.
* **Tags/Categories:**  
  * Users should be able to add custom tags or select from predefined categories.  
* **Search and Filtering:**  
  * Users should be able to search their logged whiskies by distillery, name, region, and tags.  
  * Users should be able to filter their whiskies by various criteria including:
    * Country of origin (Scotland, United States, Japan, etc.)
    * Region (Speyside, Islay, Kentucky, etc.)
    * Whiskey category/type (Single Malt, Bourbon, Rye, etc.)
    * Production attributes (peated, chill-filtered, colored)
    * Age statement range
    * ABV range
    * Rating range (1-5 stars)
    * For American whiskeys: mashbill composition (high-rye, wheated, etc.)
    * Purchase date range
    * Price range
  * The system should support compound filtering (e.g., "show me all peated Islay single malts rated 4 stars or higher")
  * Filtering interfaces should be intuitive and provide educational context where appropriate
  * Filter selections should dynamically update the display without requiring page reloads
  * Users should be able to save frequently used filter combinations
* **Whiskey Detail View:**  
  * Users should be able to view a dedicated page for each logged whiskey.  
  * This page should display all the information entered for that whiskey (distillery, region, age, etc.).  
  * This page should display all the general tasting notes recorded for that whiskey.  
  * This page should also display the bottle image. \<-- Added display of bottle image  
  * This page should also display a list of all tasting sessions in which that whiskey was included, with the session-specific notes and the order it was tasted in.  
* **Dashboard/Summary:**  
  * The system should provide a customizable dashboard where users can select which analytics and insights they want to display.
  * Available analytics modules should include:
    * Collection overview (count by type, region, distillery)
    * Taste preference analysis (showing flavor profiles the user tends to rate highest)
    * Collection value estimation
    * Drinking patterns over time (frequency and volume)
    * Regional or distillery preferences (based on ratings and frequency)
    * Comparative analysis between tasting sessions
    * Aging trends (correlation between age and ratings)
  * Users should be able to add, remove, and rearrange dashboard modules according to their preferences.
  * The dashboard should allow users to filter analytics by date ranges.
  * Users should be able to save their dashboard configuration.
  * A "default" dashboard configuration should be available for new users.
* **Inventory Management:**  
  * Users should be able to add whiskies from their logged entries to their inventory.  
  * Users should be able to specify the quantity of each bottle they own.  
  * Users should be able to update the quantity of bottles in their inventory.  
  * Users should be able to mark a bottle as "Consumed," which could then update the inventory and potentially link to a tasting session.  
  * Users should be able to view their current inventory.  
* **Data Export & Backup:**
  * Users should be able to export their whiskey collection data to CSV format for use in spreadsheet applications.
  * Users should be able to generate printable reports of:
    * Their whiskey collection
    * Detailed tasting notes for specific whiskies
    * Tasting session summaries
    * Infinity bottle compositions
  * The system should provide automated backup functionality to prevent data loss.
  * Users should be able to manually trigger a backup of their data.
  * Users should be able to restore their data from a previous backup.

* **Tasting Sessions:**  
  * Users should be able to create a new tasting session, optionally providing a name or theme for the session.  
  * Users should be able to add whiskies from their logged entries (or inventory) to a tasting session.  
  * Users should be able to record the order in which the whiskies were tasted within a session.  
  * For each whiskey within a session, users should be able to add tasting notes specific to that session.  
  * Users should be able to view details of past tasting sessions.  
* **Infinity Bottle Management:**  
  * Users should be able to create a new infinity bottle, giving it a name.  
  * Users should be able to add whiskies from their inventory to an infinity bottle.  
  * Users should be able to specify the date and amount of each addition to the infinity bottle.  
  * Users should be able to view the contents of an infinity bottle, including the date and amount of each addition.  
  * Users should be able to add general tasting notes to an infinity bottle.  
  * Users should be able to edit and delete infinity bottles.

**V. Non-Functional Requirements**

*(We can work on this section together. Here are some initial ideas to get us started. Think about things like)*

* **Performance:**  
  * The application should load quickly (e.g., within 2 seconds).  
  * The application should be responsive to user interactions.  
* **Scalability:**  
  * The application should be able to handle a growing number of users and whiskey entries.  
* **Security:**  
  * User data should be stored securely.  
  * The application should protect against common web vulnerabilities (e.g., SQL injection, XSS). This is especially important for image uploads.  
* **Usability:**  
  * The application should have a clear and intuitive user interface.  
  * The application should be easy to navigate.  
  * Uploading images should be straightforward and provide feedback to the user.  
* **Accessibility:**  
  * The application must conform to Web Content Accessibility Guidelines (WCAG) 2.1 Level AA standards.
  * Key accessibility requirements include:
    * Text alternatives for all non-text content (including whiskey bottle images)
    * Sufficient color contrast ratios for text and interface elements
    * Full keyboard navigation support without requiring specific timing for keystrokes
    * Clear form labels and error messages
    * Screen reader compatibility for all content and functions
    * Resizable text without loss of content or functionality
    * Multiple ways to navigate the application (search, menus, breadcrumbs)
    * Descriptive headings and labels to aid navigation
    * No content that flashes more than three times per second
  * Accessibility testing should be incorporated into the development process.
  * Documentation should include accessibility features and instructions.
* **Reliability:**  
  * The application should be available and function correctly most of the time.  
* **Connectivity:**
  * The application will require an internet connection to function.
  * Offline functionality is not a requirement for the initial release.
  * Future versions may consider offline capabilities if the application transitions to a PWA.
* **Maintainability:**  
  * The application's code should be well-organized and easy to update.

**VI. Design Considerations**

*(We can expand on these. Some initial thoughts include)*

* **User Interface (UI):**  
  * Clean and modern design.  
  * Consistent styling across all pages.  
  * Mobile-friendly and responsive layout.  
  * Easy and intuitive image upload process. Consider displaying a preview of the uploaded image.  
* **User Experience (UX):**  
  * Easy and intuitive navigation.  
  * Clear and concise labels and instructions.  
  * Efficient workflow for logging whiskies and tasting notes.  
  * Easy way to track and manage infinity bottles.  
  * Visual appeal of bottle images should enhance the user experience.  
* **Branding and Style:**  
  * Consider a color palette and typography that evokes the world of whiskey (e.g., rich browns, warm tones, classic fonts).  
  * Incorporate imagery or design elements related to whiskey.

**VII. Release Criteria**

*(We'll define this later, but it will include things like)*

* All core features are implemented and tested, including image upload.  
* The application meets the non-functional requirements (e.g., performance, security).  
* User testing has been conducted, and any critical issues have been resolved.  
* Documentation is complete.

**VIII. Future Considerations (Optional)**

*(Ideas for future enhancements)*

* **Group Collaboration Features:**
  * Ability for users to create or join whiskey groups/clubs
  * Shared inventory management within groups
  * Collaborative infinity bottles accessible to group members
  * Group tasting sessions with combined notes
  * Notifications for group activities and updates
* Integration with whiskey databases or APIs.  
* Advanced search and filtering options, including filtering by bottle image.  
* Import/export functionality.
* Whiskey recommendation system (deliberately excluded from initial scope to maintain simplicity).
* **Admin Pages:**
  * User management interface.
  * Database management (CRUD operations for the central whiskey database).
* **Identity & Security:**
  * Social Login integration (Google, Microsoft, etc.).
  * Enable HTTPS for production environment (Critical for OAuth and security).
* **Global Ratings:** Aggregate per-user tasting ratings into a community/global average per whiskey, rather than only showing individual ratings.
* **Ideas from Competitive Research (Sep 2026):** Not scoped or committed — captured for reference after surveying Distiller, Whiskybase, Whiskey Social, BAXUS, OnlyDrams, WhiskeySearcher/BoozApp, and wine-tracker equivalents (CellarTracker, Vivino). See memory `competitor_feature_research` for full source notes.
  * **Barcode/label scanning** to add a bottle to the collection (near-universal across competitors). ([#254](https://github.com/ferrinHouse/WhiskeyTracker.Web/issues/254))
  * **Market value / price-history tracking** per whiskey or bottle, beyond the single purchase price already recorded. ([#255](https://github.com/ferrinHouse/WhiskeyTracker.Web/issues/255))
  * **Wishlist** — a "want to try/buy" list distinct from owned bottles. ([#256](https://github.com/ferrinHouse/WhiskeyTracker.Web/issues/256))
  * **Collection randomizer** ("pick my pour") — suggest a bottle/whiskey to drink next from the current collection. ([#257](https://github.com/ferrinHouse/WhiskeyTracker.Web/issues/257))
  * **Rarity/breakdown ranking** of a collection (e.g. common → rare → impossible) — would require an external rarity/production-volume data source. ([#258](https://github.com/ferrinHouse/WhiskeyTracker.Web/issues/258))
  * Deliberately excluded for now: public social feed, following, venue check-ins, and blockchain/marketplace features (BAXUS-style) — these imply growing into a multi-tenant consumer product rather than a personal/small-group hobby app.

**IX. Open Issues (Optional)**

*(Any unresolved questions or decisions)*

* ~~Specific rating scale (e.g., 1-10, 1-100).~~ *Resolved: Using a 1-5 star rating scale.*
* Detailed design of the dashboard.  
* How to handle large whiskey databases for distillery/region lookups.  
* Image file size limits and supported formats.
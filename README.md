# 💬 ProfDrop

> **Say it anonymously. Make learning better.**

ProfDrop is a full-stack web application that allows students to give anonymous feedback to their lecturers throughout the semester.

The aim is to make feedback quicker, easier to give, and more useful while the course is still in progress.

---

## 📚 Table of Contents

- [Group Members](#-group-members)
- [About the Project](#-about-the-project)
- [The Problem](#-the-problem)
- [Our Solution](#-our-solution)
- [Target Users](#-target-users)
- [Main Features](#-main-features)
- [Technology Stack](#-technology-stack)
- [System Architecture](#-system-architecture)
- [Data Storage](#-data-storage)
- [Running the Project Locally](#-running-the-project-locally)
- [API Testing](#-api-testing)
- [Docker](#-docker)
- [CICD Pipeline](#-cicd-pipeline)
- [Deployment](#-deployment)
- [Future Improvements](#-future-improvements)

---
## 👩‍💻 Group Members

| Member | Main Contributions |
|---|---|
| **Hameeda Hamid** | Student feedback flow, backend/API work, Table Storage setup, testing and DevOps contribution |
| **Khadija** | Lecturer flow, authentication, Blob Storage setup, testing and DevOps contribution |

> Contributions can be updated before the final submission to match the work completed by each member.

---

## 🌱 About the Project

ProfDrop is a lecturer feedback platform designed to improve communication between students and lecturers.

Students can submit feedback without creating an account. Lecturers have a protected area where they can log in, view their courses, and read feedback linked to those courses.

The project is being developed as a full-stack cloud application using a .NET frontend and backend, cloud storage, Docker, GitHub Actions, testing, and live deployment.

---

## ❗ The Problem

Students already complete SETs to give feedback about lecturers, but these are normally only available during certain periods and have a deadline.

This means students cannot always give feedback when something happens during the semester.

The feedback also does not always go directly to the lecturer. It may first go through an administrative process and lecturers can receive a more general summary later.

This can make feedback less immediate.

Some students may also feel nervous about speaking directly to a lecturer in class.

---

## 💡 Our Solution

ProfDrop gives students a simple way to share feedback throughout the semester.

Students can:

- choose a course;
- view the lecturer linked to that course;
- select a feedback category;
- write a message; and
- submit the feedback anonymously.

Lecturers can:

- log in securely;
- view their own courses;
- open feedback for a course; and
- filter feedback by category.

The main goal is to make feedback more direct, regular, and useful while there is still time to act on it.

---

## 👥 Target Users

| User | How ProfDrop Helps |
|---|---|
| **Students** | Gives them a simple and anonymous way to share feedback throughout the semester. |
| **Lecturers** | Gives them quicker access to feedback linked to their own courses. |

---

## ✨ Main Features

### Student Features

- View available courses
- View the lecturer linked to a course
- Choose a feedback category
- Submit anonymous feedback
- Receive a success message after submission

### Lecturer Features

- Lecturer login
- Protected lecturer pages
- View assigned courses
- View course feedback
- Filter feedback by category
- Logout

---

## 🛠️ Technology Stack

| Area | Technology |
|---|---|
| Frontend | ASP.NET Core MVC |
| Backend | .NET Azure Functions using HTTP triggers |
| Local Storage | Azurite |
| Live Storage | Azure Table Storage and Azure Blob Storage |
| API Testing | Postman |
| Containerisation | Docker |
| Local Orchestration | Docker Compose |
| CI/CD | GitHub Actions |
| Version Control | Git and GitHub |
| Deployment | Azure |

---

## 🏗️ System Architecture

```text
Student / Lecturer
        ↓
ASP.NET Core MVC Frontend
        ↓
HTTP Requests
        ↓
Azure Functions Backend
        ↓
Azure Storage
   ┌───────────────┐
   ↓               ↓
Table Storage   Blob Storage
   ↓               ↓
Lecturers       Lecturer Images
Courses
Feedback
```

### Local Development

During development and testing, ProfDrop uses **Azurite** to emulate Azure Storage locally.

```text
MVC Frontend
     ↓
Azure Functions
     ↓
Azurite
```

### Live Application

For the deployed application, the backend will connect to live Azure Storage so that the data remains available after the application is restarted or redeployed.

---

## 🗃️ Data Storage

ProfDrop uses three Azure Storage tables.

### `Lecturers`

Stores lecturer information such as:

- Lecturer ID
- First name
- Last name
- Email
- Password hash
- Photo URL

### `Courses`

Stores course information such as:

- Course code
- Course name
- Lecturer ID

### `Feedback`

Stores submitted feedback such as:

- Feedback ID
- Course code
- Category
- Message
- Date submitted

Student names, student numbers, and student email addresses are not stored with feedback records.

### Blob Storage

A Blob Storage container called:

```text
lecturer-images
```

is used for lecturer profile pictures.

The lecturer record stores the image URL while the actual image is stored in Blob Storage.

---

## 💻 Running the Project Locally

### 1. Download the Project

Open the ProfDrop GitHub repository.

Click the green **Code** button and select **Download ZIP**.

Once the ZIP file has downloaded:

1. Open **File Explorer**.
2. Go to the **Downloads** folder.
3. Right-click the ProfDrop ZIP file.
4. Select **Extract All**.
5. Open the extracted ProfDrop folder.

---

### 2. Open ProfDrop in Visual Studio

Inside the ProfDrop folder, find:

```text
ProfDrop.sln
```

Double-click the solution file.

Visual Studio should open the full ProfDrop solution containing the frontend and backend projects.

Wait for Visual Studio to finish loading the projects and restoring the required packages.

---

### 3. Add the Local Settings

ProfDrop uses local configuration files for things such as storage connections and backend URLs.

The repository will contain:

```text
.env.example
```

Create a copy of this file and rename the copy to:

```text
.env
```

Add the required local values to the `.env` file.

The Azure Functions backend may also require:

```text
local.settings.json
```

This file contains local settings used by the backend.

---

### 4. Open Docker Desktop

Open **Docker Desktop**.

Wait until Docker shows that it is running.

ProfDrop uses Docker to start the frontend, backend and local storage services together.

---

### 5. Open the Terminal in Visual Studio

In Visual Studio:

1. Click **View**.
2. Select **Terminal**.

Make sure the terminal is opened in the main ProfDrop folder.

This should be the same folder that contains:

```text
docker-compose.yml
```

---

### 6. Start ProfDrop

Run:

```bash
docker compose up --build
```

Docker will build and start the ProfDrop services.

These include:

- the ASP.NET Core MVC frontend;
- the Azure Functions backend; and
- Azurite for local storage testing.

The first build may take a few minutes.

---

### 7. Open the Website

Once all the containers have started successfully, open the frontend URL shown in the terminal.

ProfDrop should now be running locally.

You can now test the student and lecturer features in the browser.

---

### 8. Stop ProfDrop

When you are finished using the application, return to the terminal and press:

```text
Ctrl + C
```

Then run:

```bash
docker compose down
```

This stops the running ProfDrop containers.

---

### 🔄 Running ProfDrop Again

The next time you want to run the project:

1. Open **Docker Desktop**.
2. Open `ProfDrop.sln` in Visual Studio.
3. Open the Visual Studio terminal.
4. Run:

```bash
docker compose up --build
```



## 🧪 API Testing

ProfDrop uses **Postman** to test the backend endpoints.

The Postman collection is stored in:

```text
tests/
└── postman/
    └── ProfDrop.postman_collection.json
```

Testing includes both successful and unsuccessful requests.

Examples include:

- getting courses;
- getting course details;
- submitting valid feedback;
- rejecting incomplete feedback;
- lecturer login;
- invalid lecturer login;
- getting lecturer courses;
- getting lecturer feedback; and
- preventing access to feedback that does not belong to the lecturer.

The Postman tests will also be included in the CI/CD process.

---

## 🐳 Docker

ProfDrop uses separate Dockerfiles for the frontend and backend.

```text
frontend/
└── ProfDrop.Web/
    └── Dockerfile

backend/
└── ProfDrop.Functions/
    └── Dockerfile
```

A root Docker Compose file is used to run the full stack locally:

```text
docker-compose.yml
```

The full application can be started with:

```bash
docker compose up --build
```

---

## ⚙️ CI/CD Pipeline

GitHub Actions is used to automate the development and deployment process.

The final workflow will include:

1. Restore and build the applications
2. Run automated tests
3. Build or validate the Docker containers
4. Run security checks
5. Deploy the application after approved code is merged into `main`

This helps catch problems before deployment and keeps the release process consistent.

---

## ☁️ Deployment

**Live URL:** 

**Deployment platform:** `Azure`

The frontend and backend will be deployed as separate containerised services.

The deployed application will use persistent cloud storage so that feedback and other application data remain available after redeployment.

---

## 🚀 Future Improvements

Possible future improvements include:

- notifications when lecturers receive new feedback;
- linking feedback to a specific class session;
- feedback trends over time;
- allowing lecturers to mark feedback as reviewed or actioned; and
- simple reporting for useful anonymous trends.

---

## 💚 ProfDrop

**Say it anonymously. Make learning better.**

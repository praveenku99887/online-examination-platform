# Ascend – Online Examination & Assessment Platform

Ascend is a full-stack online examination and assessment platform that enables students to take assessments, teachers to manage examinations, and administrators to oversee the platform. It also supports AI integration for enhanced assessment capabilities.

## 🚀 Features

- Role-based access for Students, Teachers, and Admins
- Secure authentication using JWT and HttpOnly session cookies
- Online assessment management
- Results and performance tracking
- AI integration through an OpenAI-compatible API
- SQLite database for local development
- Optional Azure SQL database support
- Optional Kafka-compatible event messaging
- Automated testing with xUnit and NUnit
- Docker and Docker Compose support
- Deployment resources for cloud environments

## 🛠️ Tech Stack

- **Frontend:** React, Vite, Tailwind CSS
- **Backend:** ASP.NET Core, C#, .NET 10
- **Data Access:** Dapper
- **Database:** SQLite, optional Azure SQL
- **Authentication:** JWT, HttpOnly cookies
- **AI:** OpenAI-compatible chat completions API
- **Messaging:** Kafka-compatible integration
- **Testing:** xUnit, NUnit
- **DevOps:** Docker, Docker Compose, Kubernetes

## 📁 Project Structure

```text
ascend/
├── backend/
│   ├── Ascend.Api/
│   ├── Ascend.Api.Tests.Shared/
│   ├── Ascend.Api.Tests.Xunit/
│   └── Ascend.Api.Tests.Nunit/
├── frontend-react/
├── deploy/
├── scripts/
├── Dockerfile
├── docker-compose.yml
├── .dockerignore
├── .env.example
├── ascend.slnx
└── README.md

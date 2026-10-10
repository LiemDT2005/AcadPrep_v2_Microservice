# CAPSTONE / COURSE PROJECT REPORT
# Report 1 – Project Introduction

**Project Name:** AcadPrep — Nền tảng hỗ trợ ôn luyện thi TOEIC trực tuyến  
**Project Code:** ACADPREP  
**Group:** PRN232_Group04 (Semester 8 - PRN232)  
**Software Type:** Web Application (Distributed Microservices Architecture)  
**Date:** October 2026  

---

## Table of Contents
- [I. Record of Changes](#i-record-of-changes)
- [II. Project Introduction](#ii-project-introduction)
  - [1. Overview](#1-overview)
    - [1.1 Project Information](#11-project-information)
    - [1.2 Project Team](#12-project-team)
  - [2. Product Background](#2-product-background)
  - [3. Existing Systems](#3-existing-systems)
    - [3.1 Study4 (study4.com)](#31-study4-study4com)
    - [3.2 Prep.vn (prep.vn)](#32-prepvn-prepvn)
    - [3.3 Comparative Analysis & Key Takeaways](#33-comparative-analysis--key-takeaways-for-acadprep)
  - [4. Business Opportunity](#4-business-opportunity)
  - [5. Software Product Vision](#5-software-product-vision)
  - [6. Project Scope & Limitations](#6-project-scope--limitations)
    - [6.1 Major Features](#61-major-features)
    - [6.2 Limitations & Exclusions](#62-limitations--exclusions)

---

## I. Record of Changes

| Date | A\* M, D | In charge | Change Description |
| :--- | :---: | :--- | :--- |
| **09/10/2026** | **A** | **AcadPrep Team** | Initial drafting and completion of Report 1: Project Introduction, Team, Product Background, Existing Systems, Business Opportunity, Vision, Scope & Major Features. |

*\*A - Added | M - Modified | D - Deleted*

---

## II. Project Introduction

### 1. Overview

#### 1.1 Project Information
- **Project Name:** AcadPrep — Online TOEIC Preparation Platform
- **Project Code:** `ACADPREP`
- **Group Name:** `PRN232_Group04` (Semester 8)
- **Software Type:** Web Application (Distributed Microservices Architecture: ASP.NET Core 9, Razor Pages, YARP API Gateway, EF Core, RabbitMQ, Redis, VNPay Payment Gateway)

#### 1.2 Project Team

| Full Name | Role & Service Responsibility | Email | Student ID / Note |
| :--- | :--- | :--- | :--- |
| **Instructor / Lecturer** | Project Supervisor | `lecturer@fe.edu.vn` | FPT University |
| **Đặng Thanh Liêm** | **Team Leader** / Exam Engine Service Lead *(Core Test Engine, Timed Scoring, Attempts History)* | `liemdt.ce190697@gmail.com` | `CE190697` (Leader) |
| **Dương Châu Đăng Khoa** | **Member** / Identity Service Lead *(Authentication, OAuth2, JWT Security, RBAC)* | `khoadcd@fe.edu.vn` | Member |
| **Hồng Tuấn Nguyên** | **Member** / Content CMS Lead *(Exam Bank, Questions, Passages, Content Publishing)* | `nguyenht@fe.edu.vn` | Member |
| **Hứa Khánh Duy** | **Member** / Billing & Notification Lead *(Freemium Quotas, VNPay Checkout, IPN, Notifications)* | `duyhk@fe.edu.vn` | Member |
| **Đỗ Hồ Gia Huy** | **Member** / Learning & Analytics Lead *(Spaced Repetition Flashcards, Streak, Leaderboard, Analytics)* | `huyddh@fe.edu.vn` | Member |

---

### 2. Product Background

In the contemporary global academic and corporate landscape, the **Test of English for International Communication (TOEIC)** serves as an indispensable benchmark for assessing workplace English proficiency. At Vietnamese universities (including FPT University, VNU, and polytechnics) and multinational enterprises, achieving a TOEIC score of 500 to 850+ is a mandatory graduation exit criterion and a pivotal prerequisite for career recruitment and advancement.

Despite the vital importance of TOEIC certification, the overwhelming majority of learners encounter substantial hurdles during their preparation journey:
- **High Costs and Inflexible Schedules:** Traditional brick-and-mortar English preparation centers impose high tuition fees and rigid schedules, which are ill-suited for busy university students and working professionals.
- **Passive Learning from Physical Textbooks:** Self-study using conventional printed books lacks interactive audio synchronization, instantaneous scoring, and detailed explanation breakdowns. Learners often spend excessive time manually cross-checking answer keys without understanding why an option was incorrect.
- **Inefficient Vocabulary Memorization:** Most learners struggle to retain technical business vocabulary due to passive rote memorization without scientifically proven methods like Spaced Repetition (SRS).
- **Monolithic Legacy Test Websites:** Many available test-prep websites suffer from sluggish response times during peak hours, uninspiring user interfaces, lack of real-time progress analytics, and absence of seamless micro-transaction subscription plans.

To decisively overcome these challenges, the AcadPrep team proposed and developed an all-in-one, highly scalable, and user-centric online TOEIC learning platform. Built upon a distributed **Microservices Architecture**, AcadPrep delivers an authentic 2-hour full test simulation, targeted part-by-part drills, intelligent SRS-based flashcard memorization, real-time performance analytics, and a seamless Freemium monetization model integrated with VNPay. This empowers learners to study anytime, anywhere, with maximum efficiency and at an affordable cost.

---

### 3. Existing Systems

To design an effective architecture and competitive feature set, our team conducted a thorough evaluation of prominent existing test preparation platforms in the market:

#### 3.1 Study4 (https://study4.com)
- **System Overview:** Study4 is a well-known Vietnamese EdTech platform offering online mock tests for TOEIC, IELTS, and JLPT, alongside spaced-repetition flashcard tools.
- **Key System Actors:** Guest (trial users), Learner (registered users/subscribers), Content Manager, Administrator.
- **Major Features:** Full-length TOEIC test rooms, part-by-part mini tests, community answer discussion forums, flashcard deck creation, and audio transcript display.
- **Strengths (Pros):** Clean web interface, extensive repository of past ETS exam papers, active learner discussion section below each question.
- **Weaknesses (Cons):** The system is built predominantly as a monolithic web application, which experiences noticeable latency and occasional service degradation during evening peak hours. Premium course packages are expensive, and analytical breakdowns between sub-skills (e.g., Inference vs. Detail questions in Part 7) are relatively limited.

#### 3.2 Prep.vn (https://prep.vn)
- **System Overview:** Prep.vn is a comprehensive smart learning platform focused on standardized language exams with interactive video courses, mock test rooms, and AI-assisted grading.
- **Key System Actors:** Learner, Academic Mentor/Teacher, Consultant, System Administrator.
- **Major Features:** Structured video learning roadmaps, virtual testing rooms with anti-cheat timers, AI Virtual Speaking/Writing grading room, and scheduled study reminders.
- **Strengths (Pros):** High pedagogical quality, well-curated study pathways, excellent marketing, and modern branding.
- **Weaknesses (Cons):** High barrier to entry with costly course bundles; lacks a dedicated lightweight freemium drill-oriented workflow for rapid daily exam practice and Spaced Repetition vocabulary lookup directly from mock tests.

#### 3.3 Comparative Analysis & Key Takeaways for AcadPrep

| Evaluation Criteria | Study4 | Prep.vn | AcadPrep (Our System) |
| :--- | :--- | :--- | :--- |
| **Core Architecture** | Monolith Web Application | Service-Oriented / Cloud Monolith | **Independent Microservices (Clean Arch, YARP, RabbitMQ, Redis)** |
| **Freemium Model** | Limited Free Tests (Ad-supported) | Minimal Free Content (Course-gated) | **Flexible Freemium (Daily free practice + affordable VNPay Pro tier)** |
| **Vocabulary & SRS** | Standard Flashcards | Vocabulary Lists per Video | **In-Test Contextual Vocabulary Notebook + SuperMemo-based SRS Rating** |
| **Test Engine & UX** | Full Test + Mini Test | Full Test Room | **Full Test 7 Parts + Part Drill + Resume + Granular Detailed Explanations** |
| **Performance & Scale** | Moderate (Peak latency spikes) | High (Heavy frontend bandwidth) | **High Resilience & Zero-downtime microservice scaling** |

---

### 4. Business Opportunity

The market demand for standardized English assessment in Vietnam has surged exponentially over the past five years. Annually, over 500,000 university students and early-career job seekers register for TOEIC examinations across major cities (Hanoi, Ho Chi Minh City, Da Nang, Can Tho). Despite high demand, there is a clear market gap between expensive physical tutoring centers (costing 3,000,000–8,000,000 VND per course) and low-engagement static PDF mock tests.

AcadPrep seizes this lucrative opportunity by introducing an agile, cost-efficient, and highly scalable online preparation platform characterized by:
1. **High-Conversion Freemium Model:** Unregistered and free-tier users can experience core features (taking selected practice parts, saving initial vocabulary, and testing platform ergonomics). When users desire unlimited full-length mock exams, comprehensive question rationales, and advanced score projections, they can instantly upgrade to Pro via VNPay with affordable micro-packages (e.g., 49,000 VND/month or 129,000 VND/quarter).
2. **Cost-Effective Microservices Infrastructure:** By leveraging lightweight .NET 9 services, Redis distributed caching, and asynchronous RabbitMQ event processing, AcadPrep maintains low server overhead while easily supporting concurrent spikes during exam seasons.
3. **High User Retention via Gamification & SRS:** Study streaks, achievement badges, and spaced repetition review schedules cultivate long-term daily learning habits, generating recurring user engagement and word-of-mouth referral growth.
4. **Seamless Domestic Payment Integration:** Direct integration with VNPay (supporting VNPay QR, domestic ATM cards, and mobile banking apps) removes payment friction for Vietnamese students who often do not possess international credit cards.

---

### 5. Software Product Vision

> *“For learners and working professionals who want an authentic, self-paced, and data-driven approach to mastering the TOEIC examination, **AcadPrep is a modern, high-performance web platform** built on a distributed microservices architecture that provides real-time exam simulations (Full Test 7 Parts & Part Drills), contextual Spaced Repetition (SRS) vocabulary flashcards, in-depth analytical performance tracking, and seamless domestic VNPay payment for Pro access. Unlike traditional monolithic websites or expensive offline test centers, **AcadPrep delivers zero-friction accessibility, uninterrupted testing with automatic countdown and resume, instant explanations, and habit-forming gamification that accelerates score gains and ensures examination success.**”*

---

### 6. Project Scope & Limitations

To ensure precise project execution within the academic semester and align with the distributed microservices architecture, the project scope, key functional capabilities, and explicit boundaries have been strictly established.

#### 6.1 Major Features

- **FE-01: User Identity & Access Management**  
  Provides secure user onboarding and authentication. Includes Email/Password registration with 6-digit OTP email verification, Google OAuth2 single sign-on, JWT-based stateless session management, secure password reset, profile management, and Role-Based Access Control (Admin, Moderator, Learner).
- **FE-02: TOEIC Exam Engine & Simulation**  
  Supports two testing modes: Full Test (200 questions across 7 Parts, 120-minute countdown, audio player with play restrictions) and Practice by Part (custom part selection without strict time pressure). Features real-time state persistence and session resumption in case of unexpected network disconnection.
- **FE-03: Instant Scoring & Comprehensive Answer Review**  
  Automatically grades submitted tests according to standardized TOEIC score conversion tables, calculates scaled Listening and Reading sub-scores, presents comprehensive question-by-question review with answer rationales, and catalogs incorrect answers into a dedicated review repository.
- **FE-04: Smart Vocabulary Notebook & Spaced Repetition (SRS)**  
  Enables learners to look up words in context and save them directly from reading passages into personal vocabulary notebooks. Integrates a SuperMemo-based Spaced Repetition algorithm (SRS Flashcards) with recall difficulty ratings (1 to 5) to optimize long-term memory retention.
- **FE-05: Learning Analytics, Gamification & Leaderboard**  
  Provides visual performance dashboards illustrating Listening vs. Reading strengths/weaknesses, score progress trends over time, daily study streak tracking (with UTC+7 timezone auto-reset), milestone achievement badges, and competitive learner leaderboards.
- **FE-06: Back-Office Content Management System (CMS)**  
  Empowers Content Moderators and Administrators to author, update, structure, and publish TOEIC exams, question banks (with audio files, transcripts, images, and explanations), and multi-question reading passages with referential integrity validation.
- **FE-07: Subscription Billing & VNPay Integration**  
  Implements a Freemium model with quota enforcement (daily limits on full tests, practice sessions, and vocabulary saves for free tier). Provides VNPay checkout integration (HMAC-SHA512 checksum, IPN webhook handling, idempotent transaction processing) for instant Pro plan activation.
- **FE-08: Real-Time System Notifications & Administrative Reporting**  
  Delivers in-app notifications for exam completion, daily study streak milestones, and subscription status. Equips administrators with comprehensive reporting dashboards covering new registrations, retention, revenue, and exam completion metrics.

#### 6.2 Limitations & Exclusions

- **LI-01: Focus on TOEIC Listening & Reading Only**  
  The current system exclusively supports the standard 2-skill TOEIC Listening & Reading format (Parts 1 to 7). TOEIC Speaking & Writing evaluations requiring subjective human grading or certified examiners are excluded from this release.
- **LI-02: Payment Gateway Boundary (VNPay in VND)**  
  Payment processing is strictly integrated with the VNPay gateway supporting Vietnamese Dong (VND) transactions (VNPay-QR, local bank ATM/iBanking). International payment gateways (Stripe, PayPal) and multi-currency billing are not included in Phase 1.
- **LI-03: Web Application Delivery (No Native Mobile Apps)**  
  AcadPrep is delivered as a fully responsive Web Application optimized for desktop, tablet, and mobile browsers. Native mobile binaries (.apk for Android or .ipa for iOS) are outside the project boundary.
- **LI-04: No Human 1-on-1 Tutoring / Live Video Streaming**  
  AcadPrep is designed as an automated, self-paced learning and mock testing system; real-time video conferencing, live classroom broadcasting, and 1-on-1 human tutoring sessions are excluded from the system scope.
- **LI-05: Single Organization / Non-Multi-Tenant Deployment**  
  The platform is deployed as a single-tenant e-learning system for AcadPrep users; white-labeling and multi-tenant school partitioning are reserved for future commercial scaling.

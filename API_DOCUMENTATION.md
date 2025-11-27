# SIESTUR API Documentation & Frontend Integration Guide

## Table of Contents
1. [System Overview](#system-overview)
2. [Authentication](#authentication)
3. [API Endpoints](#api-endpoints)
4. [Axios Setup & Configuration](#axios-setup--configuration)
5. [Complete Frontend Integration Examples](#complete-frontend-integration-examples)
6. [SignalR Real-time Integration](#signalr-real-time-integration)
7. [Error Handling](#error-handling)
8. [Best Practices](#best-practices)

---

## System Overview

SIESTUR is a real-time queue management system built with ASP.NET Core 8, featuring:

- **Real-time updates** via SignalR (WebSockets)
- **JWT authentication** with role-based access control
- **Queue management** with FIFO (First In, First Out) processing
- **Window management** for operators
- **Digital signage** with video playlist support
- **Daily statistics and reporting**

### User Roles
- **Admin**: Full system access, user management, system configuration
- **Colaborador** (Operator): Turn management, window operations

### Turn Statuses
- `PENDING`: Waiting in queue
- `CALLED`: Called to a window
- `SERVING`: Currently being served
- `DONE`: Service completed
- `SKIPPED`: Turn skipped

---

## Authentication

### JWT Token Structure

The API uses JWT (JSON Web Tokens) for authentication. After login, you receive:

```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "John Doe",
    "email": "john@example.com",
    "role": "Admin"
  }
}
```

**Token Claims:**
- `sub` (NameIdentifier): User GUID
- `email`: User email
- `role`: User role (Admin/Colaborador)
- `exp`: Token expiration timestamp

**Token Expiration:** 24 hours (configurable via `Jwt__ExpirationHours`)

---

## API Endpoints

### Base URL
```
Production: https://your-app.railway.app
Development: https://localhost:7xxx
```


It should already be on yor .env file.

---

## 1. Authentication Endpoints

### POST `/auth/login`
Login to the system and receive JWT token.

**Request:**
```json
{
  "email": "admin@siestur.com",
  "password": "SecurePassword123"
}
```

**Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "Admin User",
    "email": "admin@siestur.com",
    "role": "Admin"
  }
}
```

**Errors:**
- `400 Bad Request`: Invalid credentials
- `401 Unauthorized`: Account inactive

---

## 2. Turn Management Endpoints

### POST `/turns`
Create a new turn (ticket) in the queue.

**Authorization:** Required (Admin or Colaborador)

**Request:**
```json
{
  "startOverride": 100  // Optional: Override starting number
}
```

**Response (201 Created):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "number": 101,
  "status": "PENDING",
  "windowNumber": null,
  "createdAt": "2025-11-27T10:30:00Z",
  "calledAt": null,
  "servedAt": null,
  "skippedAt": null
}
```

**SignalR Event:** `turns:created` is broadcast to all clients

---

### GET `/turns/recent?limit=10`
Get the most recent turns created today.

**Authorization:** Required

**Query Parameters:**
- `limit` (optional): Number of turns to retrieve (1-50, default: 10)

**Response (200 OK):**
```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "number": 105,
      "status": "SERVING",
      "windowNumber": 3,
      "createdAt": "2025-11-27T10:35:00Z",
      "calledAt": "2025-11-27T10:40:00Z",
      "servedAt": "2025-11-27T10:41:00Z",
      "skippedAt": null
    }
  ]
}
```

---

### GET `/turns/pending`
Get all pending turns in FIFO order (oldest first).

**Authorization:** Required

**Response (200 OK):**
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "number": 102,
    "status": "PENDING",
    "windowNumber": null,
    "createdAt": "2025-11-27T10:30:00Z",
    "calledAt": null,
    "servedAt": null,
    "skippedAt": null
  }
]
```

---

## 3. Window Management Endpoints

### GET `/windows`
Get all windows with their current status.

**Authorization:** Required

**Response (200 OK):**
```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "number": 1,
      "name": "Ventanilla Principal",
      "active": true,
      "currentTurn": {
        "id": "abc123...",
        "number": 105,
        "status": "SERVING",
        "calledAt": "2025-11-27T10:40:00Z",
        "servedAt": "2025-11-27T10:41:00Z"
      },
      "operatorName": "John Doe"
    }
  ]
}
```

---

### POST `/windows/sessions`
Start a window session (operator logs into a window).

**Authorization:** Required

**Request:**
```json
{
  "windowNumber": 1
}
```

**Response (200 OK):**
```json
{
  "message": "Sesión de ventanilla iniciada.",
  "windowNumber": 1
}
```

**Errors:**
- `400 Bad Request`: Invalid window number
- `404 Not Found`: Window doesn't exist or is inactive
- `409 Conflict`: Window is already occupied by another operator

**SignalR Event:** `windows:updated` is broadcast

---

### POST `/windows/{windowNumber}/take-next`
Take the next pending turn from the queue.

**Authorization:** Required (must have active session on this window)

**Response (200 OK):**
```json
{
  "turnId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "turnNumber": 102
}
```

**Errors:**
- `404 Not Found`: No pending turns available
- `403 Forbidden`: No active session on this window

**SignalR Events:**
- `turns:called` broadcast with turn data
- `windows:updated` broadcast

---

### POST `/windows/{windowNumber}/serve`
Mark the current turn as being served.

**Authorization:** Required (must have active session on this window)

**Response (200 OK):**
```json
{
  "message": "Turno en atención.",
  "turnNumber": 102
}
```

**Errors:**
- `404 Not Found`: No turn called at this window
- `403 Forbidden`: No active session on this window

**SignalR Events:**
- `turns:serving` broadcast
- `windows:updated` broadcast

---

### POST `/windows/{windowNumber}/complete`
Complete the current turn (mark as DONE).

**Authorization:** Required (must have active session on this window)

**Response (200 OK):**
```json
{
  "message": "Turno completado.",
  "turnNumber": 102
}
```

**SignalR Events:**
- `turns:completed` broadcast
- `windows:updated` broadcast

---

### POST `/windows/{windowNumber}/skip`
Skip the current turn (mark as SKIPPED).

**Authorization:** Required (must have active session on this window)

**Response (200 OK):**
```json
{
  "message": "Turno omitido.",
  "turnNumber": 102
}
```

**SignalR Events:**
- `turns:skipped` broadcast
- `windows:updated` broadcast

---

### POST `/windows/sessions/end`
End the current window session (operator logs out).

**Authorization:** Required

**Response (200 OK):**
```json
{
  "message": "Sesión cerrada."
}
```

**SignalR Event:** `windows:updated` broadcast

---

## 4. Public Board Endpoints

These endpoints are **public** (no authentication required) for displaying information on digital signage.

### GET `/public-board/current`
Get current turns being served and recently called.

**Response (200 OK):**
```json
{
  "serving": [
    {
      "turnNumber": 105,
      "windowNumber": 3,
      "windowName": "Ventanilla Principal",
      "servedAt": "2025-11-27T10:41:00Z"
    }
  ],
  "recentCalled": [
    {
      "turnNumber": 104,
      "windowNumber": 2,
      "windowName": "Ventanilla 2",
      "calledAt": "2025-11-27T10:40:00Z"
    }
  ]
}
```

---

### GET `/public-board/pending-count`
Get count of pending turns.

**Response (200 OK):**
```json
{
  "count": 15
}
```

---

## 5. Admin Endpoints

All admin endpoints require **Admin** role.

### POST `/admin/users`
Create a new user.

**Request:**
```json
{
  "name": "Jane Smith",
  "email": "jane@siestur.com",
  "password": "SecurePass123",
  "role": "Colaborador"
}
```

**Response (201 Created):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Jane Smith",
  "email": "jane@siestur.com",
  "role": "Colaborador",
  "active": true
}
```

---

### GET `/admin/users`
List all users.

**Response (200 OK):**
```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "name": "Jane Smith",
      "email": "jane@siestur.com",
      "role": "Colaborador",
      "active": true,
      "createdAt": "2025-11-27T10:00:00Z"
    }
  ]
}
```

---

### PUT `/admin/users/{id}`
Update a user.

**Request:**
```json
{
  "name": "Jane Doe",
  "email": "jane.doe@siestur.com",
  "role": "Admin",
  "active": true
}
```

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Jane Doe",
  "email": "jane.doe@siestur.com",
  "role": "Admin",
  "active": true
}
```

---

### DELETE `/admin/users/{id}`
Delete a user.

**Response (200 OK):**
```json
{
  "message": "Usuario eliminado."
}
```

---

### POST `/admin/reset-day`
Reset the day (clear all turns, close sessions, reset counter).

**Request:**
```json
{
  "confirmation": "Estoy seguro de borrar los turnos."
}
```

**Response (200 OK):**
```json
{
  "message": "Día reiniciado.",
  "startDefault": 1
}
```

**SignalR Events:**
- `turns:reset` broadcast
- `windows:updated` broadcast

---

### GET `/admin/windows`
Get all windows (including inactive).

**Response (200 OK):**
```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "number": 1,
      "name": "Ventanilla Principal",
      "active": true
    }
  ]
}
```

---

### POST `/admin/windows`
Create a new window.

**Request:**
```json
{
  "number": 5,
  "name": "Ventanilla Express",
  "active": true
}
```

**Response (201 Created):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "number": 5,
  "name": "Ventanilla Express",
  "active": true
}
```

---

### PUT `/admin/windows/{id}`
Update a window.

**Request:**
```json
{
  "number": 5,
  "name": "Ventanilla VIP",
  "active": true
}
```

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "number": 5,
  "name": "Ventanilla VIP",
  "active": true
}
```

---

### DELETE `/admin/windows/{id}`
Delete a window.

**Response (200 OK):**
```json
{
  "message": "Ventanilla eliminada."
}
```

---

### GET `/admin/settings`
Get system settings.

**Response (200 OK):**
```json
{
  "startNumberDefault": 1,
  "autoResetEnabled": false,
  "autoResetHour": 0
}
```

---

### PUT `/admin/settings`
Update system settings.

**Request:**
```json
{
  "startNumberDefault": 100,
  "autoResetEnabled": true,
  "autoResetHour": 6
}
```

**Response (200 OK):**
```json
{
  "startNumberDefault": 100,
  "autoResetEnabled": true,
  "autoResetHour": 6
}
```

---

## 6. Video Playlist Endpoints

### GET `/videos`
Get all videos in playlist order.

**Authorization:** Required

**Response (200 OK):**
```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "title": "Welcome Video",
      "url": "https://youtube.com/watch?v=...",
      "position": 1,
      "active": true
    }
  ]
}
```

---

### POST `/videos`
Add a video to the playlist.

**Authorization:** Required (Admin only)

**Request:**
```json
{
  "title": "Promotional Video",
  "url": "https://youtube.com/watch?v=xyz",
  "position": 2,
  "active": true
}
```

**Response (201 Created):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "Promotional Video",
  "url": "https://youtube.com/watch?v=xyz",
  "position": 2,
  "active": true
}
```

**SignalR Event:** `videos:updated` broadcast

---

### PUT `/videos/{id}`
Update a video.

**Authorization:** Required (Admin only)

**Request:**
```json
{
  "title": "Updated Title",
  "url": "https://youtube.com/watch?v=abc",
  "position": 1,
  "active": true
}
```

**Response (200 OK):**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "title": "Updated Title",
  "url": "https://youtube.com/watch?v=abc",
  "position": 1,
  "active": true
}
```

**SignalR Event:** `videos:updated` broadcast

---

### DELETE `/videos/{id}`
Delete a video from playlist.

**Authorization:** Required (Admin only)

**Response (200 OK):**
```json
{
  "message": "Video eliminado."
}
```

**SignalR Event:** `videos:updated` broadcast

---

## 7. Statistics Endpoints

### GET `/stats/daily`
Get today's statistics.

**Authorization:** Required (Admin only)

**Response (200 OK):**
```json
{
  "date": "2025-11-27",
  "totalTurns": 150,
  "completed": 140,
  "skipped": 10,
  "pending": 5,
  "avgWaitTimeMinutes": 12.5,
  "avgServiceTimeMinutes": 5.3
}
```

---

### GET `/stats/operators`
Get operator performance statistics for today.

**Authorization:** Required (Admin only)

**Response (200 OK):**
```json
{
  "items": [
    {
      "operatorId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "operatorName": "John Doe",
      "turnsCompleted": 45,
      "turnsSkipped": 2,
      "avgServiceTimeMinutes": 4.8,
      "totalActiveTimeMinutes": 240
    }
  ]
}
```

---

### GET `/stats/range?from=2025-11-01&to=2025-11-30`
Get statistics for a date range.

**Authorization:** Required (Admin only)

**Query Parameters:**
- `from`: Start date (YYYY-MM-DD)
- `to`: End date (YYYY-MM-DD)

**Response (200 OK):**
```json
{
  "from": "2025-11-01",
  "to": "2025-11-30",
  "totalTurns": 4500,
  "completed": 4200,
  "skipped": 300,
  "avgWaitTimeMinutes": 15.2,
  "avgServiceTimeMinutes": 6.1,
  "dailyBreakdown": [
    {
      "date": "2025-11-01",
      "totalTurns": 150,
      "completed": 140,
      "skipped": 10
    }
  ]
}
```

---

## Axios Setup & Configuration

### Installation

```bash
npm install axios @microsoft/signalr
```

---

### 1. Axios Instance Configuration

Create `src/api/axios.js`:

```javascript
import axios from 'axios';

// Base URL configuration
const BASE_URL = import.meta.env.VITE_API_URL || 'https://your-app.railway.app';

// Create axios instance
const apiClient = axios.create({
  baseURL: BASE_URL,
  timeout: 30000, // 30 seconds
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor - Add JWT token to all requests
apiClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('authToken');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => {
    return Promise.reject(error);
  }
);

// Response interceptor - Handle errors globally
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response) {
      // Server responded with error status
      switch (error.response.status) {
        case 401:
          // Unauthorized - clear token and redirect to login
          localStorage.removeItem('authToken');
          localStorage.removeItem('user');
          window.location.href = '/login';
          break;
        case 403:
          // Forbidden - user doesn't have permission
          console.error('Access forbidden:', error.response.data);
          break;
        case 404:
          // Not found
          console.error('Resource not found:', error.response.data);
          break;
        case 409:
          // Conflict (e.g., window already occupied)
          console.error('Conflict:', error.response.data);
          break;
        case 500:
          // Server error
          console.error('Server error:', error.response.data);
          break;
        default:
          console.error('API Error:', error.response.data);
      }
    } else if (error.request) {
      // Request was made but no response received
      console.error('Network error - no response received');
    } else {
      // Something else happened
      console.error('Error:', error.message);
    }
    return Promise.reject(error);
  }
);

export default apiClient;
```

---

### 2. Environment Variables

Create `.env` file:

```env
# Production
VITE_API_URL=https://your-app.railway.app

# Development
# VITE_API_URL=https://localhost:7xxx
```

---

### 3. Authentication Service

Create `src/services/authService.js`:

```javascript
import apiClient from '../api/axios';

class AuthService {
  /**
   * Login user and store JWT token
   * @param {string} email
   * @param {string} password
   * @returns {Promise<{token: string, user: object}>}
   */
  async login(email, password) {
    try {
      const response = await apiClient.post('/auth/login', {
        email,
        password,
      });

      const { token, user } = response.data;

      // Store token and user in localStorage
      localStorage.setItem('authToken', token);
      localStorage.setItem('user', JSON.stringify(user));

      return { token, user };
    } catch (error) {
      throw new Error(
        error.response?.data?.message || 'Login failed. Please check your credentials.'
      );
    }
  }

  /**
   * Logout user
   */
  logout() {
    localStorage.removeItem('authToken');
    localStorage.removeItem('user');
    window.location.href = '/login';
  }

  /**
   * Get current user from localStorage
   * @returns {object|null}
   */
  getCurrentUser() {
    const userJson = localStorage.getItem('user');
    return userJson ? JSON.parse(userJson) : null;
  }

  /**
   * Check if user is authenticated
   * @returns {boolean}
   */
  isAuthenticated() {
    return !!localStorage.getItem('authToken');
  }

  /**
   * Check if current user is admin
   * @returns {boolean}
   */
  isAdmin() {
    const user = this.getCurrentUser();
    return user?.role === 'Admin';
  }

  /**
   * Get authentication token
   * @returns {string|null}
   */
  getToken() {
    return localStorage.getItem('authToken');
  }
}

export default new AuthService();
```

---

### 4. Turn Service

Create `src/services/turnService.js`:

```javascript
import apiClient from '../api/axios';

class TurnService {
  /**
   * Create a new turn
   * @param {number|null} startOverride - Optional starting number override
   * @returns {Promise<object>}
   */
  async createTurn(startOverride = null) {
    try {
      const response = await apiClient.post('/turns', {
        startOverride,
      });
      return response.data;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to create turn');
    }
  }

  /**
   * Get recent turns
   * @param {number} limit - Number of turns to retrieve (1-50)
   * @returns {Promise<Array>}
   */
  async getRecentTurns(limit = 10) {
    try {
      const response = await apiClient.get('/turns/recent', {
        params: { limit },
      });
      return response.data.items;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to fetch recent turns');
    }
  }

  /**
   * Get pending turns
   * @returns {Promise<Array>}
   */
  async getPendingTurns() {
    try {
      const response = await apiClient.get('/turns/pending');
      return response.data;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to fetch pending turns');
    }
  }
}

export default new TurnService();
```

---

### 5. Window Service

Create `src/services/windowService.js`:

```javascript
import apiClient from '../api/axios';

class WindowService {
  /**
   * Get all windows with current status
   * @returns {Promise<Array>}
   */
  async getWindows() {
    try {
      const response = await apiClient.get('/windows');
      return response.data.items;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to fetch windows');
    }
  }

  /**
   * Start a window session (operator login to window)
   * @param {number} windowNumber
   * @returns {Promise<object>}
   */
  async startSession(windowNumber) {
    try {
      const response = await apiClient.post('/windows/sessions', {
        windowNumber,
      });
      return response.data;
    } catch (error) {
      if (error.response?.status === 409) {
        throw new Error('This window is already occupied by another operator');
      }
      throw new Error(error.response?.data?.message || 'Failed to start session');
    }
  }

  /**
   * End current window session
   * @returns {Promise<object>}
   */
  async endSession() {
    try {
      const response = await apiClient.post('/windows/sessions/end');
      return response.data;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to end session');
    }
  }

  /**
   * Take next pending turn
   * @param {number} windowNumber
   * @returns {Promise<object>}
   */
  async takeNext(windowNumber) {
    try {
      const response = await apiClient.post(`/windows/${windowNumber}/take-next`);
      return response.data;
    } catch (error) {
      if (error.response?.status === 404) {
        throw new Error('No pending turns available');
      }
      if (error.response?.status === 403) {
        throw new Error('You do not have an active session on this window');
      }
      throw new Error(error.response?.data?.message || 'Failed to take next turn');
    }
  }

  /**
   * Mark current turn as serving
   * @param {number} windowNumber
   * @returns {Promise<object>}
   */
  async serve(windowNumber) {
    try {
      const response = await apiClient.post(`/windows/${windowNumber}/serve`);
      return response.data;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to serve turn');
    }
  }

  /**
   * Complete current turn
   * @param {number} windowNumber
   * @returns {Promise<object>}
   */
  async complete(windowNumber) {
    try {
      const response = await apiClient.post(`/windows/${windowNumber}/complete`);
      return response.data;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to complete turn');
    }
  }

  /**
   * Skip current turn
   * @param {number} windowNumber
   * @returns {Promise<object>}
   */
  async skip(windowNumber) {
    try {
      const response = await apiClient.post(`/windows/${windowNumber}/skip`);
      return response.data;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to skip turn');
    }
  }
}

export default new WindowService();
```

---

### 6. Admin Service

Create `src/services/adminService.js`:

```javascript
import apiClient from '../api/axios';

class AdminService {
  // ===== USER MANAGEMENT =====

  /**
   * Get all users
   * @returns {Promise<Array>}
   */
  async getUsers() {
    try {
      const response = await apiClient.get('/admin/users');
      return response.data.items;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to fetch users');
    }
  }

  /**
   * Create new user
   * @param {object} userData - {name, email, password, role}
   * @returns {Promise<object>}
   */
  async createUser(userData) {
    try {
      const response = await apiClient.post('/admin/users', userData);
      return response.data;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to create user');
    }
  }

  /**
   * Update user
   * @param {string} userId
   * @param {object} userData - {name, email, role, active}
   * @returns {Promise<object>}
   */
  async updateUser(userId, userData) {
    try {
      const response = await apiClient.put(`/admin/users/${userId}`, userData);
      return response.data;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to update user');
    }
  }

  /**
   * Delete user
   * @param {string} userId
   * @returns {Promise<object>}
   */
  async deleteUser(userId) {
    try {
      const response = await apiClient.delete(`/admin/users/${userId}`);
      return response.data;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to delete user');
    }
  }

  // ===== WINDOW MANAGEMENT =====

  /**
   * Get all windows (including inactive)
   * @returns {Promise<Array>}
   */
  async getAllWindows() {
    try {
      const response = await apiClient.get('/admin/windows');
      return response.data.items;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to fetch windows');
    }
  }

  /**
   * Create new window
   * @param {object} windowData - {number, name, active}
   * @returns {Promise<object>}
   */
  async createWindow(windowData) {
    try {
      const response = await apiClient.post('/admin/windows', windowData);
      return response.data;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to create window');
    }
  }

  /**
   * Update window
   * @param {string} windowId
   * @param {object} windowData - {number, name, active}
   * @returns {Promise<object>}
   */
  async updateWindow(windowId, windowData) {
    try {
      const response = await apiClient.put(`/admin/windows/${windowId}`, windowData);
      return response.data;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to update window');
    }
  }

  /**
   * Delete window
   * @param {string} windowId
   * @returns {Promise<object>}
   */
  async deleteWindow(windowId) {
    try {
      const response = await apiClient.delete(`/admin/windows/${windowId}`);
      return response.data;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to delete window');
    }
  }

  // ===== SYSTEM OPERATIONS =====

  /**
   * Reset day (clear all turns, close sessions, reset counter)
   * @returns {Promise<object>}
   */
  async resetDay() {
    try {
      const response = await apiClient.post('/admin/reset-day', {
        confirmation: 'Estoy seguro de borrar los turnos.',
      });
      return response.data;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to reset day');
    }
  }

  /**
   * Get system settings
   * @returns {Promise<object>}
   */
  async getSettings() {
    try {
      const response = await apiClient.get('/admin/settings');
      return response.data;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to fetch settings');
    }
  }

  /**
   * Update system settings
   * @param {object} settings - {startNumberDefault, autoResetEnabled, autoResetHour}
   * @returns {Promise<object>}
   */
  async updateSettings(settings) {
    try {
      const response = await apiClient.put('/admin/settings', settings);
      return response.data;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to update settings');
    }
  }
}

export default new AdminService();
```

---

### 7. Public Board Service

Create `src/services/publicBoardService.js`:

```javascript
import axios from 'axios';

const BASE_URL = import.meta.env.VITE_API_URL || 'https://your-app.railway.app';

// Public endpoints don't need authentication
const publicClient = axios.create({
  baseURL: BASE_URL,
  timeout: 10000,
});

class PublicBoardService {
  /**
   * Get current turns (serving and recently called)
   * @returns {Promise<object>}
   */
  async getCurrentTurns() {
    try {
      const response = await publicClient.get('/public-board/current');
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch current turns');
    }
  }

  /**
   * Get count of pending turns
   * @returns {Promise<number>}
   */
  async getPendingCount() {
    try {
      const response = await publicClient.get('/public-board/pending-count');
      return response.data.count;
    } catch (error) {
      throw new Error('Failed to fetch pending count');
    }
  }
}

export default new PublicBoardService();
```

---

### 8. Video Service

Create `src/services/videoService.js`:

```javascript
import apiClient from '../api/axios';

class VideoService {
  /**
   * Get all videos in playlist
   * @returns {Promise<Array>}
   */
  async getVideos() {
    try {
      const response = await apiClient.get('/videos');
      return response.data.items;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to fetch videos');
    }
  }

  /**
   * Create new video
   * @param {object} videoData - {title, url, position, active}
   * @returns {Promise<object>}
   */
  async createVideo(videoData) {
    try {
      const response = await apiClient.post('/videos', videoData);
      return response.data;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to create video');
    }
  }

  /**
   * Update video
   * @param {string} videoId
   * @param {object} videoData - {title, url, position, active}
   * @returns {Promise<object>}
   */
  async updateVideo(videoId, videoData) {
    try {
      const response = await apiClient.put(`/videos/${videoId}`, videoData);
      return response.data;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to update video');
    }
  }

  /**
   * Delete video
   * @param {string} videoId
   * @returns {Promise<object>}
   */
  async deleteVideo(videoId) {
    try {
      const response = await apiClient.delete(`/videos/${videoId}`);
      return response.data;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to delete video');
    }
  }
}

export default new VideoService();
```

---

### 9. Statistics Service

Create `src/services/statsService.js`:

```javascript
import apiClient from '../api/axios';

class StatsService {
  /**
   * Get today's statistics
   * @returns {Promise<object>}
   */
  async getDailyStats() {
    try {
      const response = await apiClient.get('/stats/daily');
      return response.data;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to fetch daily stats');
    }
  }

  /**
   * Get operator performance statistics
   * @returns {Promise<Array>}
   */
  async getOperatorStats() {
    try {
      const response = await apiClient.get('/stats/operators');
      return response.data.items;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to fetch operator stats');
    }
  }

  /**
   * Get statistics for date range
   * @param {string} from - Start date (YYYY-MM-DD)
   * @param {string} to - End date (YYYY-MM-DD)
   * @returns {Promise<object>}
   */
  async getRangeStats(from, to) {
    try {
      const response = await apiClient.get('/stats/range', {
        params: { from, to },
      });
      return response.data;
    } catch (error) {
      throw new Error(error.response?.data?.message || 'Failed to fetch range stats');
    }
  }
}

export default new StatsService();
```

---

## SignalR Real-time Integration

### 1. SignalR Connection Service

Create `src/services/signalrService.js`:

```javascript
import * as signalR from '@microsoft/signalr';
import authService from './authService';

const BASE_URL = import.meta.env.VITE_API_URL || 'https://your-app.railway.app';

class SignalRService {
  constructor() {
    this.turnsConnection = null;
    this.windowsConnection = null;
    this.videosConnection = null;
  }

  /**
   * Initialize SignalR connection with authentication
   * @param {string} hubPath - Hub endpoint path
   * @returns {HubConnection}
   */
  createConnection(hubPath) {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${BASE_URL}${hubPath}`, {
        accessTokenFactory: () => authService.getToken(),
        skipNegotiation: true,
        transport: signalR.HttpTransportType.WebSockets,
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Exponential backoff: 0s, 2s, 10s, 30s, then 30s
          if (retryContext.previousRetryCount === 0) return 0;
          if (retryContext.previousRetryCount === 1) return 2000;
          if (retryContext.previousRetryCount === 2) return 10000;
          return 30000;
        },
      })
      .configureLogging(signalR.LogLevel.Information)
      .build();

    // Connection event handlers
    connection.onreconnecting((error) => {
      console.warn(`SignalR reconnecting: ${error}`);
    });

    connection.onreconnected((connectionId) => {
      console.log(`SignalR reconnected: ${connectionId}`);
    });

    connection.onclose((error) => {
      console.error(`SignalR connection closed: ${error}`);
    });

    return connection;
  }

  /**
   * Start Turns Hub connection
   * @returns {Promise<HubConnection>}
   */
  async startTurnsHub() {
    if (this.turnsConnection?.state === signalR.HubConnectionState.Connected) {
      return this.turnsConnection;
    }

    this.turnsConnection = this.createConnection('/hubs/turns');

    try {
      await this.turnsConnection.start();
      console.log('Turns Hub connected');
      return this.turnsConnection;
    } catch (error) {
      console.error('Failed to start Turns Hub:', error);
      throw error;
    }
  }

  /**
   * Start Windows Hub connection
   * @returns {Promise<HubConnection>}
   */
  async startWindowsHub() {
    if (this.windowsConnection?.state === signalR.HubConnectionState.Connected) {
      return this.windowsConnection;
    }

    this.windowsConnection = this.createConnection('/hubs/windows');

    try {
      await this.windowsConnection.start();
      console.log('Windows Hub connected');
      return this.windowsConnection;
    } catch (error) {
      console.error('Failed to start Windows Hub:', error);
      throw error;
    }
  }

  /**
   * Start Videos Hub connection
   * @returns {Promise<HubConnection>}
   */
  async startVideosHub() {
    if (this.videosConnection?.state === signalR.HubConnectionState.Connected) {
      return this.videosConnection;
    }

    this.videosConnection = this.createConnection('/hubs/videos');

    try {
      await this.videosConnection.start();
      console.log('Videos Hub connected');
      return this.videosConnection;
    } catch (error) {
      console.error('Failed to start Videos Hub:', error);
      throw error;
    }
  }

  /**
   * Stop all connections
   */
  async stopAll() {
    const promises = [];

    if (this.turnsConnection) {
      promises.push(this.turnsConnection.stop());
    }
    if (this.windowsConnection) {
      promises.push(this.windowsConnection.stop());
    }
    if (this.videosConnection) {
      promises.push(this.videosConnection.stop());
    }

    await Promise.all(promises);
    console.log('All SignalR connections stopped');
  }

  /**
   * Subscribe to Turns Hub events
   * @param {object} handlers - Event handler functions
   */
  onTurnsEvents(handlers) {
    if (!this.turnsConnection) {
      throw new Error('Turns Hub not connected. Call startTurnsHub() first.');
    }

    if (handlers.created) {
      this.turnsConnection.on('turns:created', handlers.created);
    }
    if (handlers.called) {
      this.turnsConnection.on('turns:called', handlers.called);
    }
    if (handlers.serving) {
      this.turnsConnection.on('turns:serving', handlers.serving);
    }
    if (handlers.completed) {
      this.turnsConnection.on('turns:completed', handlers.completed);
    }
    if (handlers.skipped) {
      this.turnsConnection.on('turns:skipped', handlers.skipped);
    }
    if (handlers.reset) {
      this.turnsConnection.on('turns:reset', handlers.reset);
    }
  }

  /**
   * Subscribe to Windows Hub events
   * @param {Function} handler - Event handler function
   */
  onWindowsUpdated(handler) {
    if (!this.windowsConnection) {
      throw new Error('Windows Hub not connected. Call startWindowsHub() first.');
    }

    this.windowsConnection.on('windows:updated', handler);
  }

  /**
   * Subscribe to Videos Hub events
   * @param {Function} handler - Event handler function
   */
  onVideosUpdated(handler) {
    if (!this.videosConnection) {
      throw new Error('Videos Hub not connected. Call startVideosHub() first.');
    }

    this.videosConnection.on('videos:updated', handler);
  }
}

export default new SignalRService();
```

---

## Complete Frontend Integration Examples

### Example 1: Login Page (React)

```jsx
import React, { useState } from 'react';
import authService from '../services/authService';

function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleLogin = async (e) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      const { user } = await authService.login(email, password);

      // Redirect based on role
      if (user.role === 'Admin') {
        window.location.href = '/admin/dashboard';
      } else {
        window.location.href = '/operator/dashboard';
      }
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-container">
      <h1>SIESTUR Login</h1>
      <form onSubmit={handleLogin}>
        <div>
          <label>Email:</label>
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
          />
        </div>
        <div>
          <label>Password:</label>
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
        </div>
        {error && <div className="error">{error}</div>}
        <button type="submit" disabled={loading}>
          {loading ? 'Logging in...' : 'Login'}
        </button>
      </form>
    </div>
  );
}

export default LoginPage;
```

---

### Example 2: Turn Assignment Page (React)

```jsx
import React, { useState, useEffect } from 'react';
import turnService from '../services/turnService';
import signalrService from '../services/signalrService';

function TurnAssignmentPage() {
  const [recentTurns, setRecentTurns] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    // Load initial data
    loadRecentTurns();

    // Start SignalR connection
    initializeSignalR();

    // Cleanup on unmount
    return () => {
      signalrService.stopAll();
    };
  }, []);

  const loadRecentTurns = async () => {
    try {
      const turns = await turnService.getRecentTurns(20);
      setRecentTurns(turns);
    } catch (err) {
      setError(err.message);
    }
  };

  const initializeSignalR = async () => {
    try {
      await signalrService.startTurnsHub();

      // Subscribe to real-time events
      signalrService.onTurnsEvents({
        created: (turn) => {
          console.log('New turn created:', turn);
          setRecentTurns((prev) => [turn, ...prev].slice(0, 20));
        },
        called: (turn) => {
          console.log('Turn called:', turn);
          updateTurnInList(turn);
        },
        serving: (turn) => {
          console.log('Turn serving:', turn);
          updateTurnInList(turn);
        },
        completed: (turn) => {
          console.log('Turn completed:', turn);
          updateTurnInList(turn);
        },
        skipped: (turn) => {
          console.log('Turn skipped:', turn);
          updateTurnInList(turn);
        },
        reset: () => {
          console.log('Day reset');
          setRecentTurns([]);
        },
      });
    } catch (err) {
      console.error('SignalR connection failed:', err);
    }
  };

  const updateTurnInList = (updatedTurn) => {
    setRecentTurns((prev) =>
      prev.map((turn) => (turn.id === updatedTurn.id ? updatedTurn : turn))
    );
  };

  const createTurn = async () => {
    setLoading(true);
    setError('');

    try {
      const newTurn = await turnService.createTurn();
      console.log('Turn created:', newTurn);
      // SignalR will handle UI update via 'turns:created' event
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="turn-assignment-page">
      <h1>Asignación de Turnos</h1>

      <button onClick={createTurn} disabled={loading}>
        {loading ? 'Creando...' : 'Crear Nuevo Turno'}
      </button>

      {error && <div className="error">{error}</div>}

      <div className="recent-turns">
        <h2>Turnos Recientes</h2>
        <table>
          <thead>
            <tr>
              <th>Número</th>
              <th>Estado</th>
              <th>Ventanilla</th>
              <th>Creado</th>
            </tr>
          </thead>
          <tbody>
            {recentTurns.map((turn) => (
              <tr key={turn.id}>
                <td>{turn.number}</td>
                <td>{turn.status}</td>
                <td>{turn.windowNumber || '-'}</td>
                <td>{new Date(turn.createdAt).toLocaleTimeString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default TurnAssignmentPage;
```

---

### Example 3: Window Operator Page (React)

```jsx
import React, { useState, useEffect } from 'react';
import windowService from '../services/windowService';
import signalrService from '../services/signalrService';

function WindowOperatorPage() {
  const [currentWindow, setCurrentWindow] = useState(null);
  const [currentTurn, setCurrentTurn] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    // Initialize SignalR
    initializeSignalR();

    return () => {
      signalrService.stopAll();
    };
  }, []);

  const initializeSignalR = async () => {
    try {
      await signalrService.startWindowsHub();
      await signalrService.startTurnsHub();

      signalrService.onWindowsUpdated(() => {
        console.log('Windows updated');
        // Optionally refresh window state
      });

      signalrService.onTurnsEvents({
        called: (turn) => {
          if (currentWindow && turn.windowNumber === currentWindow.number) {
            setCurrentTurn(turn);
          }
        },
        serving: (turn) => {
          if (currentWindow && turn.windowNumber === currentWindow.number) {
            setCurrentTurn(turn);
          }
        },
        completed: () => {
          setCurrentTurn(null);
        },
        skipped: () => {
          setCurrentTurn(null);
        },
      });
    } catch (err) {
      console.error('SignalR failed:', err);
    }
  };

  const startSession = async (windowNumber) => {
    setLoading(true);
    setError('');

    try {
      const result = await windowService.startSession(windowNumber);
      setCurrentWindow({ number: windowNumber });
      console.log(result.message);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const endSession = async () => {
    setLoading(true);
    setError('');

    try {
      await windowService.endSession();
      setCurrentWindow(null);
      setCurrentTurn(null);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const takeNext = async () => {
    setLoading(true);
    setError('');

    try {
      const result = await windowService.takeNext(currentWindow.number);
      console.log(`Llamando turno ${result.turnNumber}`);
      // SignalR will update currentTurn via 'turns:called' event
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const serve = async () => {
    setLoading(true);
    setError('');

    try {
      await windowService.serve(currentWindow.number);
      // SignalR will update via 'turns:serving' event
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const complete = async () => {
    setLoading(true);
    setError('');

    try {
      await windowService.complete(currentWindow.number);
      // SignalR will clear currentTurn via 'turns:completed' event
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const skip = async () => {
    setLoading(true);
    setError('');

    try {
      await windowService.skip(currentWindow.number);
      // SignalR will clear currentTurn via 'turns:skipped' event
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  if (!currentWindow) {
    return (
      <div className="window-operator-page">
        <h1>Seleccione una Ventanilla</h1>
        {error && <div className="error">{error}</div>}
        <div className="window-selection">
          {[1, 2, 3, 4, 5].map((num) => (
            <button key={num} onClick={() => startSession(num)} disabled={loading}>
              Ventanilla {num}
            </button>
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="window-operator-page">
      <h1>Ventanilla {currentWindow.number}</h1>
      <button onClick={endSession}>Cerrar Sesión</button>

      {error && <div className="error">{error}</div>}

      {currentTurn ? (
        <div className="current-turn">
          <h2>Turno Actual: {currentTurn.number}</h2>
          <p>Estado: {currentTurn.status}</p>

          <div className="actions">
            {currentTurn.status === 'CALLED' && (
              <button onClick={serve} disabled={loading}>
                Atender
              </button>
            )}
            {currentTurn.status === 'SERVING' && (
              <>
                <button onClick={complete} disabled={loading}>
                  Completar
                </button>
                <button onClick={skip} disabled={loading}>
                  Omitir
                </button>
              </>
            )}
          </div>
        </div>
      ) : (
        <div className="no-turn">
          <p>Sin turno asignado</p>
          <button onClick={takeNext} disabled={loading}>
            Llamar Siguiente
          </button>
        </div>
      )}
    </div>
  );
}

export default WindowOperatorPage;
```

---

### Example 4: Public Board Display (React)

```jsx
import React, { useState, useEffect } from 'react';
import publicBoardService from '../services/publicBoardService';
import signalrService from '../services/signalrService';

function PublicBoardPage() {
  const [serving, setServing] = useState([]);
  const [recentCalled, setRecentCalled] = useState([]);
  const [pendingCount, setPendingCount] = useState(0);

  useEffect(() => {
    loadCurrentTurns();
    loadPendingCount();

    // Start SignalR for real-time updates
    initializeSignalR();

    // Refresh every 10 seconds as fallback
    const interval = setInterval(() => {
      loadCurrentTurns();
      loadPendingCount();
    }, 10000);

    return () => {
      clearInterval(interval);
      signalrService.stopAll();
    };
  }, []);

  const loadCurrentTurns = async () => {
    try {
      const data = await publicBoardService.getCurrentTurns();
      setServing(data.serving);
      setRecentCalled(data.recentCalled);
    } catch (err) {
      console.error('Failed to load current turns:', err);
    }
  };

  const loadPendingCount = async () => {
    try {
      const count = await publicBoardService.getPendingCount();
      setPendingCount(count);
    } catch (err) {
      console.error('Failed to load pending count:', err);
    }
  };

  const initializeSignalR = async () => {
    try {
      // Public board doesn't need authentication for listening
      await signalrService.startTurnsHub();
      await signalrService.startWindowsHub();

      signalrService.onTurnsEvents({
        called: () => loadCurrentTurns(),
        serving: () => loadCurrentTurns(),
        completed: () => {
          loadCurrentTurns();
          loadPendingCount();
        },
        skipped: () => {
          loadCurrentTurns();
          loadPendingCount();
        },
        created: () => loadPendingCount(),
        reset: () => {
          setServing([]);
          setRecentCalled([]);
          setPendingCount(0);
        },
      });

      signalrService.onWindowsUpdated(() => {
        loadCurrentTurns();
      });
    } catch (err) {
      console.error('SignalR failed:', err);
    }
  };

  return (
    <div className="public-board">
      <h1>SIESTUR - Sistema de Turnos</h1>

      <div className="stats">
        <div className="pending-count">
          <h2>Turnos Pendientes</h2>
          <p className="count">{pendingCount}</p>
        </div>
      </div>

      <div className="serving-section">
        <h2>Atendiendo Ahora</h2>
        <div className="serving-list">
          {serving.length === 0 ? (
            <p>No hay turnos en atención</p>
          ) : (
            serving.map((turn, idx) => (
              <div key={idx} className="turn-card serving">
                <div className="turn-number">{turn.turnNumber}</div>
                <div className="window-info">
                  <div className="window-number">Ventanilla {turn.windowNumber}</div>
                  <div className="window-name">{turn.windowName}</div>
                </div>
              </div>
            ))
          )}
        </div>
      </div>

      <div className="called-section">
        <h2>Llamados Recientes</h2>
        <div className="called-list">
          {recentCalled.length === 0 ? (
            <p>No hay llamados recientes</p>
          ) : (
            recentCalled.map((turn, idx) => (
              <div key={idx} className="turn-card called">
                <div className="turn-number">{turn.turnNumber}</div>
                <div className="window-info">
                  <div className="window-number">Ventanilla {turn.windowNumber}</div>
                  <div className="window-name">{turn.windowName}</div>
                </div>
              </div>
            ))
          )}
        </div>
      </div>
    </div>
  );
}

export default PublicBoardPage;
```

---

### Example 5: Admin Dashboard (React)

```jsx
import React, { useState, useEffect } from 'react';
import adminService from '../services/adminService';
import statsService from '../services/statsService';

function AdminDashboard() {
  const [users, setUsers] = useState([]);
  const [windows, setWindows] = useState([]);
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    setLoading(true);
    try {
      const [usersData, windowsData, statsData] = await Promise.all([
        adminService.getUsers(),
        adminService.getAllWindows(),
        statsService.getDailyStats(),
      ]);

      setUsers(usersData);
      setWindows(windowsData);
      setStats(statsData);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleResetDay = async () => {
    const confirmed = window.confirm(
      '¿Está seguro de reiniciar el día? Esto borrará todos los turnos actuales.'
    );

    if (!confirmed) return;

    setLoading(true);
    setError('');

    try {
      const result = await adminService.resetDay();
      alert(result.message);
      loadData(); // Reload data
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleCreateUser = async (userData) => {
    setLoading(true);
    setError('');

    try {
      await adminService.createUser(userData);
      loadData(); // Reload users
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleDeleteUser = async (userId) => {
    const confirmed = window.confirm('¿Está seguro de eliminar este usuario?');
    if (!confirmed) return;

    setLoading(true);
    setError('');

    try {
      await adminService.deleteUser(userId);
      loadData(); // Reload users
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  if (loading && !stats) {
    return <div>Cargando...</div>;
  }

  return (
    <div className="admin-dashboard">
      <h1>Panel de Administración</h1>

      {error && <div className="error">{error}</div>}

      {/* Statistics */}
      {stats && (
        <div className="stats-section">
          <h2>Estadísticas de Hoy</h2>
          <div className="stats-grid">
            <div className="stat-card">
              <h3>Total Turnos</h3>
              <p>{stats.totalTurns}</p>
            </div>
            <div className="stat-card">
              <h3>Completados</h3>
              <p>{stats.completed}</p>
            </div>
            <div className="stat-card">
              <h3>Omitidos</h3>
              <p>{stats.skipped}</p>
            </div>
            <div className="stat-card">
              <h3>Pendientes</h3>
              <p>{stats.pending}</p>
            </div>
            <div className="stat-card">
              <h3>Tiempo Espera Promedio</h3>
              <p>{stats.avgWaitTimeMinutes?.toFixed(1)} min</p>
            </div>
            <div className="stat-card">
              <h3>Tiempo Atención Promedio</h3>
              <p>{stats.avgServiceTimeMinutes?.toFixed(1)} min</p>
            </div>
          </div>
        </div>
      )}

      {/* System Actions */}
      <div className="actions-section">
        <h2>Acciones del Sistema</h2>
        <button onClick={handleResetDay} className="danger" disabled={loading}>
          Reiniciar Día
        </button>
      </div>

      {/* Users Management */}
      <div className="users-section">
        <h2>Gestión de Usuarios</h2>
        <table>
          <thead>
            <tr>
              <th>Nombre</th>
              <th>Email</th>
              <th>Rol</th>
              <th>Estado</th>
              <th>Acciones</th>
            </tr>
          </thead>
          <tbody>
            {users.map((user) => (
              <tr key={user.id}>
                <td>{user.name}</td>
                <td>{user.email}</td>
                <td>{user.role}</td>
                <td>{user.active ? 'Activo' : 'Inactivo'}</td>
                <td>
                  <button onClick={() => handleDeleteUser(user.id)}>Eliminar</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Windows Management */}
      <div className="windows-section">
        <h2>Gestión de Ventanillas</h2>
        <table>
          <thead>
            <tr>
              <th>Número</th>
              <th>Nombre</th>
              <th>Estado</th>
            </tr>
          </thead>
          <tbody>
            {windows.map((window) => (
              <tr key={window.id}>
                <td>{window.number}</td>
                <td>{window.name}</td>
                <td>{window.active ? 'Activa' : 'Inactiva'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export default AdminDashboard;
```

---

## Error Handling

### Common Error Scenarios

| Status Code | Meaning | Common Causes | Frontend Action |
|-------------|---------|---------------|-----------------|
| 400 | Bad Request | Invalid input data, validation failed | Show validation error to user |
| 401 | Unauthorized | Missing/invalid token, token expired | Redirect to login, clear stored token |
| 403 | Forbidden | User lacks permission for action | Show "Access Denied" message |
| 404 | Not Found | Resource doesn't exist, no pending turns | Show "Not Found" message |
| 409 | Conflict | Window already occupied, duplicate operation | Show conflict message, refresh state |
| 500 | Server Error | Unexpected server error | Show generic error, retry option |

### Example Error Handler Component (React)

```jsx
function ErrorBoundary({ error, onRetry, onDismiss }) {
  if (!error) return null;

  const getMessage = (err) => {
    if (err.response?.status === 401) {
      return 'Su sesión ha expirado. Por favor, inicie sesión nuevamente.';
    }
    if (err.response?.status === 403) {
      return 'No tiene permisos para realizar esta acción.';
    }
    if (err.response?.status === 404) {
      return 'El recurso solicitado no fue encontrado.';
    }
    if (err.response?.status === 409) {
      return 'Conflicto: ' + (err.response?.data?.message || 'Operación no permitida.');
    }
    return err.message || 'Ha ocurrido un error inesperado.';
  };

  return (
    <div className="error-boundary">
      <div className="error-content">
        <h3>Error</h3>
        <p>{getMessage(error)}</p>
        <div className="error-actions">
          {onRetry && <button onClick={onRetry}>Reintentar</button>}
          {onDismiss && <button onClick={onDismiss}>Cerrar</button>}
        </div>
      </div>
    </div>
  );
}
```

---

## Best Practices

### 1. Token Management
- Always store JWT token in `localStorage` (or `sessionStorage` for higher security)
- Attach token to every authenticated request via Axios interceptor
- Clear token and redirect to login on 401 errors
- Implement token refresh if needed (requires backend support)

### 2. Real-time Updates
- Initialize SignalR connections early in app lifecycle
- Always handle reconnection scenarios
- Use SignalR for UI updates instead of polling
- Fallback to polling only for public displays

### 3. Error Handling
- Implement global error handler via Axios interceptor
- Show user-friendly error messages
- Log errors for debugging
- Provide retry mechanisms for transient failures

### 4. Performance
- Use React.memo() for components that receive SignalR updates
- Debounce rapid state updates
- Limit list sizes (use pagination or virtual scrolling)
- Lazy load routes and components

### 5. Security
- Never log sensitive data (tokens, passwords)
- Validate all user input on frontend AND backend
- Use HTTPS in production
- Implement CORS properly on backend

### 6. State Management
- Consider using Redux/Zustand for complex state
- Keep SignalR connection state separate from UI state
- Use React Context for auth state
- Normalize data structures (avoid nested objects)

---

## Environment-Specific Configuration

### Development (.env.development)
```env
VITE_API_URL=https://localhost:7xxx
VITE_SIGNALR_LOG_LEVEL=Information
```

### Production (.env.production)
```env
VITE_API_URL=https://your-app.railway.app
VITE_SIGNALR_LOG_LEVEL=Warning
```

---

## Testing Examples

### Testing API Service (Jest)

```javascript
import turnService from '../services/turnService';
import apiClient from '../api/axios';

jest.mock('../api/axios');

describe('TurnService', () => {
  afterEach(() => {
    jest.clearAllMocks();
  });

  test('createTurn should return new turn', async () => {
    const mockTurn = {
      id: '123',
      number: 101,
      status: 'PENDING',
      createdAt: '2025-11-27T10:00:00Z',
    };

    apiClient.post.mockResolvedValue({ data: mockTurn });

    const result = await turnService.createTurn();

    expect(apiClient.post).toHaveBeenCalledWith('/turns', { startOverride: null });
    expect(result).toEqual(mockTurn);
  });

  test('createTurn should throw error on failure', async () => {
    apiClient.post.mockRejectedValue({
      response: { data: { message: 'Server error' } },
    });

    await expect(turnService.createTurn()).rejects.toThrow('Server error');
  });
});
```

---

## Conclusion

This documentation provides a complete guide for integrating the SIESTUR backend API with any frontend framework. The examples use React, but the Axios services and SignalR integration can be adapted to Vue, Angular, or vanilla JavaScript.

**Key Takeaways:**
- Use the provided Axios services for all API calls
- Implement SignalR for real-time updates
- Handle errors gracefully with user-friendly messages
- Follow security best practices for token management
- Test your API integrations thoroughly

For questions or issues, refer to the backend API source code or contact the development team.

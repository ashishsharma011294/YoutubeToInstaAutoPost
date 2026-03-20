# API Endpoints Reference

## 🚀 Application Status
✅ **Application is running on:** `http://localhost:5000`

## 📋 Available Endpoints

### 1. Swagger UI (Interactive API Documentation)
- **URL:** http://localhost:5000
- **Description:** Interactive Swagger UI for testing all endpoints
- **Access:** Open in your browser

### 2. Swagger JSON
- **URL:** http://localhost:5000/swagger/v1/swagger.json
- **Description:** OpenAPI specification in JSON format

### 3. Health Check
- **Method:** GET
- **URL:** http://localhost:5000/health
- **Description:** Check if the API is running
- **Response:**
  ```json
  {
    "status": "healthy",
    "timestamp": "2026-02-05T..."
  }
  ```

### 4. Post Reel (Main Endpoint)
- **Method:** POST
- **URL:** http://localhost:5000/post-reel
- **Content-Type:** application/json
- **Request Body:**
  ```json
  {
    "youtubeUrl": "https://www.youtube.com/watch?v=VIDEO_ID",
    "caption": "Your reel caption here"
  }
  ```
- **Success Response (200):**
  ```json
  {
    "success": true,
    "reelId": "INSTAGRAM_REEL_ID",
    "message": "Reel posted successfully"
  }
  ```
- **Error Response (500):**
  ```json
  {
    "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
    "title": "Error posting reel",
    "status": 500,
    "detail": "Error message details"
  }
  ```

## 🧪 Testing the API

### Using Swagger UI (Recommended)
1. Open your browser and navigate to: **http://localhost:5000**
2. You'll see the Swagger UI with all available endpoints
3. Click on `/post-reel` endpoint
4. Click "Try it out"
5. Enter your request body:
   ```json
   {
     "youtubeUrl": "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
     "caption": "Test reel"
   }
   ```
6. Click "Execute"
7. View the response

### Using PowerShell
```powershell
$body = @{
    youtubeUrl = "https://www.youtube.com/watch?v=VIDEO_ID"
    caption = "My reel caption"
} | ConvertTo-Json

Invoke-RestMethod -Uri http://localhost:5000/post-reel -Method Post -Body $body -ContentType "application/json"
```

### Using curl (Command Prompt)
```cmd
curl -X POST http://localhost:5000/post-reel ^
  -H "Content-Type: application/json" ^
  -d "{\"youtubeUrl\":\"https://www.youtube.com/watch?v=VIDEO_ID\",\"caption\":\"My reel caption\"}"
```

### Using Postman
1. Create a new POST request
2. URL: `http://localhost:5000/post-reel`
3. Headers: `Content-Type: application/json`
4. Body (raw JSON):
   ```json
   {
     "youtubeUrl": "https://www.youtube.com/watch?v=VIDEO_ID",
     "caption": "My reel caption"
   }
   ```
5. Click Send

## 📝 Notes

- Make sure **Ngrok** is running before testing the `/post-reel` endpoint
- The application automatically detects your Ngrok URL
- Videos are downloaded to `wwwroot/temp/` directory
- Make sure `yt-dlp.exe` and `ffmpeg.exe` are in your PATH for video downloads
- Configure your Instagram credentials in `appsettings.json` before posting reels

## 🔍 Troubleshooting

- **404 Not Found:** Make sure the application is running (`dotnet run`)
- **500 Internal Server Error:** Check logs for detailed error messages
- **Video download fails:** Verify yt-dlp and FFmpeg are installed and in PATH
- **Instagram API errors:** Verify your access token and business ID in appsettings.json

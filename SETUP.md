# Setup Instructions

## Prerequisites Installation

### 1. Install yt-dlp
1. Download `yt-dlp.exe` from: https://github.com/yt-dlp/yt-dlp/releases
2. Add it to your System PATH or place it in a folder that's in your PATH

### 2. Install FFmpeg
1. Download FFmpeg from: https://ffmpeg.org/download.html
2. Extract and add the `bin` folder to your System PATH

### 3. Install Ngrok
1. Download from: https://ngrok.com/
2. Install and authenticate: `ngrok config add-authtoken YOUR_TOKEN`

## Configuration

1. Edit `appsettings.json` and add your Instagram credentials:
   ```json
   {
     "InstagramSettings": {
       "BusinessId": "YOUR_INSTAGRAM_BUSINESS_ID",
       "AccessToken": "YOUR_LONG_LIVED_ACCESS_TOKEN",
       "NgrokBaseUrl": ""
     }
   }
   ```

## Running the Application

### Terminal 1: Start Ngrok Tunnel
```powershell
ngrok http 5000
```

### Terminal 2: Run the Application
```powershell
dotnet restore
dotnet run
```

The application will:
- Start on `http://localhost:5000`
- Automatically detect your Ngrok public URL
- Serve static files from `wwwroot/temp`

## Testing the API

### Using curl:
```powershell
curl -X POST http://localhost:5000/post-reel `
  -H "Content-Type: application/json" `
  -d '{\"youtubeUrl\":\"https://www.youtube.com/watch?v=VIDEO_ID\",\"caption\":\"My reel caption\"}'
```

### Using Postman:
1. Method: POST
2. URL: `http://localhost:5000/post-reel`
3. Body (JSON):
   ```json
   {
     "youtubeUrl": "https://www.youtube.com/watch?v=VIDEO_ID",
     "caption": "My reel caption"
   }
   ```

## Getting Instagram Credentials

1. **Business ID**: 
   - Go to Facebook Business Settings
   - Navigate to Instagram Accounts
   - Find your account and copy the Instagram Account ID

2. **Long-lived Access Token**:
   - Use Facebook Graph API Explorer
   - Generate a long-lived token with `instagram_basic`, `instagram_content_publish`, and `pages_read_engagement` permissions
   - Or use Facebook Marketing API to generate tokens programmatically

## Troubleshooting

- **Ngrok not detected**: Make sure Ngrok is running on port 4040 and accessible at `http://127.0.0.1:4040/api/tunnels`
- **Video download fails**: Ensure `yt-dlp.exe` and `ffmpeg.exe` are in your PATH
- **Instagram API errors**: Verify your access token has the correct permissions and hasn't expired

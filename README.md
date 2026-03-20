# InstaAutoPost - YouTube Shorts to Instagram Reels API

A .NET 9 Minimal API that automatically downloads YouTube videos and posts them as Instagram Reels using the Instagram Graph API.

## Prerequisites

Before running this project, ensure you have the following installed and configured:

1. **yt-dlp**: Download the `.exe` from [GitHub](https://github.com/yt-dlp/yt-dlp/releases) and add it to your System PATH
2. **FFmpeg**: Download from [ffmpeg.org](https://ffmpeg.org/download.html) and add it to your System PATH
3. **Ngrok**: Install from [ngrok.com](https://ngrok.com/)

## Configuration

1. Update `appsettings.json` with your Instagram credentials:
   ```json
   {
     "InstagramSettings": {
       "BusinessId": "YOUR_INSTAGRAM_ID",
       "AccessToken": "YOUR_LONG_LIVED_TOKEN",
       "NgrokBaseUrl": ""
     }
   }
   ```

2. Get your Instagram credentials:
   - **BusinessId**: Your Instagram Business Account ID
   - **AccessToken**: A long-lived access token from Facebook Graph API

## How to Run

### Terminal 1: Start Ngrok Tunnel
```bash
ngrok http 5000
```

### Terminal 2: Run the Application
```bash
dotnet restore
dotnet run
```

The application will:
- Automatically detect your Ngrok public URL on startup
- Update the configuration with the Ngrok base URL
- Serve static files from `wwwroot/temp`

## API Endpoint

### POST `/post-reel`

Posts a YouTube video as an Instagram Reel.

**Request Body:**
```json
{
  "youtubeUrl": "https://www.youtube.com/watch?v=VIDEO_ID",
  "caption": "Your reel caption here"
}
```

**Response:**
```json
{
  "success": true,
  "reelId": "INSTAGRAM_REEL_ID",
  "message": "Reel posted successfully"
}
```

## How It Works

1. **Download**: Uses `yt-dlp` to download the best vertical MP4 from YouTube
2. **Tunnel Automation**: Background service automatically fetches Ngrok URL from `http://127.0.0.1:4040/api/tunnels`
3. **Instagram Post**: 
   - Creates a media container via Instagram Graph API
   - Waits 30 seconds for processing
   - Publishes the reel

## Project Structure

- `Program.cs` - Main application entry point and endpoint definitions
- `Services/NgrokService.cs` - Background service for Ngrok URL management
- `Services/YouTubeDownloadService.cs` - Handles YouTube video downloads
- `Services/InstagramService.cs` - Handles Instagram API interactions
- `Models/ReelRequest.cs` - Request model for the API endpoint

## Notes

- Videos are downloaded to `wwwroot/temp/` directory
- The Ngrok service checks for URL updates every 5 minutes
- Make sure your Instagram account has the necessary permissions for posting Reels

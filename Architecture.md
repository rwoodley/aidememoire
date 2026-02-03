 ## Architecture                                                                                                                                
                                                                                                                                                 
  ### Runtime                                                                                                                                    
                                                                                                                                                 
          User (Browser)                                                                                                                         
              │                                                                                                                                  
              ▼                                                                                                                                  
          AWS App Runner (aidememoire)                                                                                                           
              │   .NET 9 Web API serving both                                                                                                    
              │   static HTML pages and REST API                                                                                                 
              │                                                                                                                                  
              ▼                                                                                                                                  
          AWS S3 (aidememoire108 bucket)                                                                                                         
              └── 1111/                                                                                                                          
                  ├── german.csv                                                                                                                 
                  ├── french.csv                                                                                                                 
                  └── {bucket-name}.csv                                                                                                          
                                                                                                                                                 
  - **App Runner** hosts a .NET 9 Web API in a Docker container                                                                                  
  - The app serves static HTML pages (`index.html`, `upload.html`) and a REST API (`/api/pairs/*`)                                               
  - **S3** stores key-value pairs as CSV files, one file per bucket at `1111/{bucket-name}.csv`                                                  
  - AWS credentials are provided via the App Runner instance role (no keys in code)                                                              
                                                                                                                                                 
  ### Deployment                                                                                                                                 
                                                                                                                                                 
          Push to GitHub (main)                                                                                                                  
              │                                                                                                                                  
              ▼                                                                                                                                  
          GitHub Actions                                                                                                                         
              │   Builds Docker image (linux/amd64)                                                                                              
              │   Pushes to Amazon ECR                                                                                                           
              │                                                                                                                                  
              ▼                                                                                                                                  
          Amazon ECR (aidememoire)                                                                                                               
              │                                                                                                                                  
              ▼                                                                                                                                  
          AWS App Runner (auto-deploy on new image)                                                                                              
                                                                                                                                                 
  - Pushing to `main` triggers a GitHub Actions workflow                                                                                         
  - The workflow builds the Docker image and pushes it to **Amazon ECR**                                                                         
  - **App Runner** detects the new image and automatically redeploys                        



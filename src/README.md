# aidememoire.app

Simple .Net 9 memorization app. 


## To Deploy

Overview:
- AWS AppRunner runs `aidememoire:latest` container from ECR
- Deployed automatically from Github on every push
- Github Actions handles the build

Steps:

```

aws ecr get-login-password --region us-east-1 --profile apprunner-deployer | docker login --username AWS --password-stdin 445753231799.dkr.ecr.us-east-1.amazonaws.com

# Build for x86_64/AMD64 (App Runner's architecture)
docker build --platform linux/amd64 -t 445753231799.dkr.ecr.us-east-1.amazonaws.com/aidememoire:latest .

# do we need this everytime? green acres doesn't do it
# docker tag aidememoire:latest 445753231799.dkr.ecr.us-east-1.amazonaws.com/aidememoire:latest

docker push 445753231799.dkr.ecr.us-east-1.amazonaws.com/aidememoire:latest
```

## To Run Locally:

```
docker run -p 8081:8080 \
  -e AWS_ACCESS_KEY_ID=$(aws configure get aws_access_key_id) \
  -e AWS_SECRET_ACCESS_KEY=$(aws configure get aws_secret_access_key) \
  -e AWS_REGION=us-east-1 \
  aidememoire
```

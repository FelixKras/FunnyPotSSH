FROM debian:stable-slim

# Install prerequisites
RUN apt-get update && apt-get install -y \
    wget \
    apt-transport-https \
    ca-certificates \
    gnupg \
    openssh-server

# Add Microsoft package signing key and feed
RUN wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    rm packages-microsoft-prod.deb

# Install .NET Runtime 8
RUN apt-get update && apt-get install -y \
    dotnet-runtime-8.0 && \
    rm -rf /var/lib/apt/lists/*


# Configure SSH
RUN mkdir /var/run/sshd

# Create a restricted user and group
RUN groupadd sshuser && \
    useradd -m -g sshuser -s /bin/bash sshuser && \
    mkdir -p /home/sshuser/app

# Copy your dotnet application to a chroot-like directory
COPY app/ /home/sshuser/app/
RUN chown -R sshuser:sshuser /home/sshuser/app && \
    chmod -R 755 /home/sshuser/app


# Copy SSH configuration
COPY sshd_config /etc/ssh/sshd_config
RUN chmod 600 /etc/ssh/sshd_config

# Secure permissions
RUN chown root:root /home/sshuser && chmod 755 /home/sshuser

# Expose SSH port
EXPOSE 22422

#adjust permissions
COPY run-app.sh /usr/local/bin/run-app.sh
RUN chmod 555 /usr/local/bin/run-app.sh && chown root:root /usr/local/bin/run-app.sh

HEALTHCHECK --interval=30s --timeout=10s CMD nc -z localhost 22422 || exit 1

# Start SSH service securely in foreground
CMD ["/usr/local/bin/run-app.sh","start-sshd"]

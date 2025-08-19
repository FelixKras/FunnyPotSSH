FROM debian:stable-slim

ARG USERNAME=test
ENV SSH_PASSWORD=test

# Install prerequisites
RUN apt-get update && apt-get install -y \
    wget apt-transport-https ca-certificates gnupg openssh-server passwd && \
    rm -rf /var/lib/apt/lists/*

# Install Microsoft package repository and .NET 8 runtime
RUN wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    rm packages-microsoft-prod.deb && \
    apt-get update && apt-get install -y dotnet-runtime-8.0 && \
    rm -rf /var/lib/apt/lists/*

RUN mkdir -p /var/run/sshd

RUN groupadd $USERNAME && \
    useradd -m -g $USERNAME -s /bin/bash $USERNAME && \
    mkdir -p /home/$USERNAME/app

# Copy app artifacts (framework-dependent DLL or self-contained binary) at runtime via volume or during build
COPY app/ /home/$USERNAME/app/
RUN chown -R $USERNAME:$USERNAME /home/$USERNAME/app && \
    chmod -R 755 /home/$USERNAME/app

# Restrict SCP/SFTP and any non-interactive commands via wrapper
RUN printf '#!/bin/sh\n\
case "$SSH_ORIGINAL_COMMAND" in\n\
  scp*|-t*|-f*|sftp*) echo "operation not allowed"; exit 1 ;;\n\
  "") exec /usr/bin/dotnet /home/%s/app/FunnyPot.dll ;;\n\
  *) echo "operation not allowed"; exit 1 ;;\n\
esac\n' $USERNAME > /usr/local/bin/restrict.sh && \
    chmod 555 /usr/local/bin/restrict.sh && \
    chown root:root /usr/local/bin/restrict.sh

RUN printf 'Port 22422\nPermitRootLogin no\nPermitEmptyPasswords no\n\
PasswordAuthentication yes\nPubkeyAuthentication yes\nPermitUserEnvironment no\n\n\
Match User %s\n    ForceCommand /usr/local/bin/restrict.sh\n    AllowTcpForwarding no\n    X11Forwarding no\n    PermitTTY yes\n' \
$USERNAME > /etc/ssh/sshd_config && chmod 600 /etc/ssh/sshd_config

RUN chown root:root /home/$USERNAME && chmod 755 /home/$USERNAME

EXPOSE 22422

# Generate run-app.sh dynamically at build time
RUN printf '#!/bin/bash\n\
set -e\n\
echo "[INFO] Container started"\n\
echo "%s:%s" | chpasswd\n\
echo "[INFO] SSH Server starting"\n\
/usr/sbin/sshd -D -e' $USERNAME $SSH_PASSWORD > /usr/local/bin/run-app.sh && \
chmod 555 /usr/local/bin/run-app.sh && \
chown root:root /usr/local/bin/run-app.sh

HEALTHCHECK --interval=30s --timeout=10s CMD pgrep -x sshd > /dev/null || exit 1

CMD ["/usr/local/bin/run-app.sh"]

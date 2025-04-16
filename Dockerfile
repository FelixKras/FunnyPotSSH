FROM debian:stable-slim

ARG USERNAME=pi
ENV SSH_PASSWORD=raspberry

# Install prerequisites
RUN apt-get update && apt-get install -y \
    wget apt-transport-https ca-certificates gnupg openssh-server passwd

RUN wget https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    rm packages-microsoft-prod.deb

RUN apt-get update && apt-get install -y dotnet-runtime-8.0 && \
    rm -rf /var/lib/apt/lists/*

RUN mkdir /var/run/sshd

RUN groupadd $USERNAME && \
    useradd -m -g $USERNAME -s /bin/bash $USERNAME && \
    mkdir -p /home/$USERNAME/app

COPY app/ /home/$USERNAME/app/
RUN chown -R $USERNAME:$USERNAME /home/$USERNAME/app && \
    chmod -R 755 /home/$USERNAME/app

RUN printf 'Port 22422\nPermitRootLogin no\nPermitEmptyPasswords no\n\
PasswordAuthentication yes\n\n\
Match User %s\n    ForceCommand /usr/bin/dotnet /home/%s/app/FunnyPot.dll\n    AllowTcpForwarding no\n    X11Forwarding no\n    PermitTTY yes\n' \
$USERNAME $USERNAME> /etc/ssh/sshd_config && chmod 600 /etc/ssh/sshd_config

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

#include <errno.h>
#include <sys/socket.h>
#include <fcntl.h>
#include <signal.h>
#include <stdlib.h>
#include <string.h>
#include <sys/ioctl.h>
#include <sys/wait.h>
#include <termios.h>
#include <unistd.h>

#if defined(__APPLE__)
#include <util.h>
#else
#include <pty.h>
#endif

int cove_pty_abi_version(void) {
    return 1;
}

int cove_pty_spawn(const char *path,
                   char *const argv[],
                   char *const envp[],
                   const char *cwd,
                   unsigned short cols,
                   unsigned short rows,
                   int *out_master_fd,
                   int *out_pid) {
    struct winsize ws;
    ws.ws_row = rows;
    ws.ws_col = cols;
    ws.ws_xpixel = 0;
    ws.ws_ypixel = 0;

    int master_fd = -1;
    pid_t pid = forkpty(&master_fd, NULL, NULL, &ws);
    if (pid < 0) {
        return -errno;
    }

    if (pid == 0) {
        /* KEEP: async-signal-safe only from here to execve; no malloc/setenv/managed code (post-fork constraint). */
        if (cwd != NULL) {
            if (chdir(cwd) != 0) {
                _exit(127);
            }
        }
        execve(path, argv, envp);
        _exit(127);
    }

    int flags = fcntl(master_fd, F_GETFD, 0);
    if (flags != -1) {
        fcntl(master_fd, F_SETFD, flags | FD_CLOEXEC);
    }

    *out_master_fd = master_fd;
    *out_pid = (int)pid;
    return 0;
}

long cove_pty_read(int fd, unsigned char *buf, int len) {
    for (;;) {
        ssize_t n = read(fd, buf, (size_t)len);
        if (n >= 0) {
            return (long)n;
        }
        if (errno == EINTR) {
            continue;
        }
        if (errno == EIO) {
            return 0;
        }
        return -(long)errno;
    }
}

long cove_pty_write(int fd, const unsigned char *buf, int len) {
    size_t total = (size_t)len;
    size_t off = 0;
    while (off < total) {
        ssize_t n = write(fd, buf + off, total - off);
        if (n < 0) {
            if (errno == EINTR) {
                continue;
            }
            return -(long)errno;
        }
        off += (size_t)n;
    }
    return (long)off;
}

int cove_pty_resize(int fd, unsigned short cols, unsigned short rows) {
    struct winsize ws;
    ws.ws_row = rows;
    ws.ws_col = cols;
    ws.ws_xpixel = 0;
    ws.ws_ypixel = 0;
    if (ioctl(fd, TIOCSWINSZ, &ws) != 0) {
        return -errno;
    }
    return 0;
}

int cove_pty_kill(int pid, int sig) {
    if (kill((pid_t)pid, sig) != 0) {
        return -errno;
    }
    return 0;
}

int cove_pty_reap(int pid) {
    int status = 0;
    pid_t r = waitpid((pid_t)pid, &status, WNOHANG);
    if (r == 0) {
        return -1;
    }
    if (r < 0) {
        return -2;
    }
    if (WIFEXITED(status)) {
        return WEXITSTATUS(status);
    }
    if (WIFSIGNALED(status)) {
        return 128 + WTERMSIG(status);
    }
    return -2;
}

void cove_pty_close(int fd) {
    close(fd);
}

int cove_pty_socketpair(int *out_a, int *out_b) {
    int fds[2];
    if (socketpair(AF_UNIX, SOCK_STREAM, 0, fds) != 0) {
        return -errno;
    }
    *out_a = fds[0];
    *out_b = fds[1];
    return 0;
}

long cove_pty_send_with_fd(int sock, const unsigned char *buf, int len, int fd) {
    struct iovec iov;
    iov.iov_base = (void *)buf;
    iov.iov_len = (size_t)len;
    struct msghdr msg;
    memset(&msg, 0, sizeof msg);
    msg.msg_iov = &iov;
    msg.msg_iovlen = 1;
    char control[CMSG_SPACE(sizeof(int))];
    if (fd >= 0) {
        memset(control, 0, sizeof control);
        msg.msg_control = control;
        msg.msg_controllen = sizeof control;
        struct cmsghdr *cmsg = CMSG_FIRSTHDR(&msg);
        cmsg->cmsg_level = SOL_SOCKET;
        cmsg->cmsg_type = SCM_RIGHTS;
        cmsg->cmsg_len = CMSG_LEN(sizeof(int));
        memcpy(CMSG_DATA(cmsg), &fd, sizeof(int));
    }
    for (;;) {
        ssize_t n = sendmsg(sock, &msg, 0);
        if (n >= 0) {
            return (long)n;
        }
        if (errno == EINTR) {
            continue;
        }
        return -(long)errno;
    }
}

long cove_pty_recv_with_fd(int sock, unsigned char *buf, int len, int *out_fd) {
    *out_fd = -1;
    struct iovec iov;
    iov.iov_base = (void *)buf;
    iov.iov_len = (size_t)len;
    struct msghdr msg;
    memset(&msg, 0, sizeof msg);
    msg.msg_iov = &iov;
    msg.msg_iovlen = 1;
    char control[CMSG_SPACE(sizeof(int))];
    memset(control, 0, sizeof control);
    msg.msg_control = control;
    msg.msg_controllen = sizeof control;
    for (;;) {
        ssize_t n = recvmsg(sock, &msg, 0);
        if (n < 0) {
            if (errno == EINTR) {
                continue;
            }
            return -(long)errno;
        }
        struct cmsghdr *cmsg = CMSG_FIRSTHDR(&msg);
        if (cmsg != NULL && cmsg->cmsg_level == SOL_SOCKET && cmsg->cmsg_type == SCM_RIGHTS
            && cmsg->cmsg_len >= CMSG_LEN(sizeof(int))) {
            memcpy(out_fd, CMSG_DATA(cmsg), sizeof(int));
        }
        return (long)n;
    }
}

int cove_pty_dup(int fd) {
    int copy = fcntl(fd, F_DUPFD_CLOEXEC, 0);
    return copy < 0 ? -errno : copy;
}

#include <poll.h>

int cove_pty_poll_readable(int fd, int timeout_ms) {
    struct pollfd p;
    p.fd = fd;
    p.events = POLLIN;
    p.revents = 0;
    for (;;) {
        int rc = poll(&p, 1, timeout_ms);
        if (rc > 0) {
            return 1;
        }
        if (rc == 0) {
            return 0;
        }
        if (errno == EINTR) {
            continue;
        }
        return -errno;
    }
}

#if defined(__APPLE__)
#include <sys/event.h>

int cove_pty_exitwatch_new(void) {
    int kq = kqueue();
    return kq < 0 ? -errno : kq;
}

int cove_pty_exitwatch_add(int wfd, int pid) {
    struct kevent kev;
    EV_SET(&kev, (uintptr_t)pid, EVFILT_PROC, EV_ADD | EV_ONESHOT, NOTE_EXIT, 0, NULL);
    if (kevent(wfd, &kev, 1, NULL, 0, NULL) < 0) {
        return errno == ESRCH ? 1 : -errno;
    }
    return 0;
}

int cove_pty_exitwatch_next(int wfd) {
    for (;;) {
        struct kevent out;
        int n = kevent(wfd, NULL, 0, &out, 1, NULL);
        if (n > 0) {
            return (int)out.ident;
        }
        if (n < 0 && errno == EINTR) {
            continue;
        }
        return -errno;
    }
}
#else
#include <sys/epoll.h>
#include <sys/syscall.h>

int cove_pty_exitwatch_new(void) {
    int ep = epoll_create1(EPOLL_CLOEXEC);
    return ep < 0 ? -errno : ep;
}

int cove_pty_exitwatch_add(int wfd, int pid) {
    int pfd = (int)syscall(SYS_pidfd_open, pid, 0);
    if (pfd < 0) {
        return errno == ESRCH ? 1 : -errno;
    }
    struct epoll_event ev;
    ev.events = EPOLLIN;
    ev.data.u64 = ((unsigned long long)(unsigned int)pid << 32) | (unsigned int)pfd;
    if (epoll_ctl(wfd, EPOLL_CTL_ADD, pfd, &ev) < 0) {
        int e = errno;
        close(pfd);
        return -e;
    }
    return 0;
}

int cove_pty_exitwatch_next(int wfd) {
    for (;;) {
        struct epoll_event out;
        int n = epoll_wait(wfd, &out, 1, -1);
        if (n > 0) {
            int pid = (int)(out.data.u64 >> 32);
            int pfd = (int)(out.data.u64 & 0xffffffffu);
            close(pfd);
            return pid;
        }
        if (n < 0 && errno == EINTR) {
            continue;
        }
        return -errno;
    }
}
#endif

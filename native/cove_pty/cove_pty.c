#include <errno.h>
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

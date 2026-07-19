#include <errno.h>
#include <sys/socket.h>
#include <fcntl.h>
#include <signal.h>
#include <pthread.h>
#include <stdint.h>
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
    return 2;
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
    pid_t r;
    for (;;) {
        r = waitpid((pid_t)pid, &status, 0);
        if (r >= 0 || errno != EINTR) {
            break;
        }
    }
    if (r < 0) {
        return -errno;
    }
    if (WIFEXITED(status)) {
        return WEXITSTATUS(status);
    }
    if (WIFSIGNALED(status)) {
        return 128 + WTERMSIG(status);
    }
    return -EIO;
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
            if ((p.revents & POLLNVAL) != 0) {
                return -EBADF;
            }
            if ((p.revents & (POLLIN | POLLHUP | POLLERR)) != 0) {
                return 1;
            }
            return 0;
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
#else
#include <sys/epoll.h>
#include <sys/eventfd.h>
#include <sys/syscall.h>
#endif

struct cove_exitwatch_registration {
    int pid;
    int native_fd;
    int64_t token;
    struct cove_exitwatch_registration *next;
};

struct cove_exitwatch {
    int fd;
#if !defined(__APPLE__)
    int wake_fd;
#endif
    pthread_mutex_t mutex;
    pthread_cond_t readers_drained;
    pthread_cond_t reader_entered;
    struct cove_exitwatch_registration *registrations;
    unsigned int active_readers;
    unsigned int reader_waiters;
    int closing;
};

static struct cove_exitwatch_registration **cove_exitwatch_find(
    struct cove_exitwatch *watch,
    int64_t token) {
    struct cove_exitwatch_registration **entry = &watch->registrations;
    while (*entry != NULL && (*entry)->token != token) {
        entry = &(*entry)->next;
    }
    return entry;
}

intptr_t cove_pty_exitwatch_new(void) {
#if defined(__APPLE__)
    int fd = kqueue();
#else
    int fd = epoll_create1(EPOLL_CLOEXEC);
#endif
    if (fd < 0) {
        return -errno;
    }
#if defined(__APPLE__)
    struct kevent wake_event;
    EV_SET(&wake_event, 1, EVFILT_USER, EV_ADD | EV_ENABLE, 0, 0, NULL);
    if (kevent(fd, &wake_event, 1, NULL, 0, NULL) < 0) {
        int e = errno;
        close(fd);
        return -e;
    }
#else
    int wake_fd = eventfd(0, EFD_CLOEXEC | EFD_NONBLOCK);
    if (wake_fd < 0) {
        int e = errno;
        close(fd);
        return -e;
    }
    struct epoll_event wake_event;
    memset(&wake_event, 0, sizeof(wake_event));
    wake_event.events = EPOLLIN;
    if (epoll_ctl(fd, EPOLL_CTL_ADD, wake_fd, &wake_event) < 0) {
        int e = errno;
        close(wake_fd);
        close(fd);
        return -e;
    }
#endif
    struct cove_exitwatch *watch = calloc(1, sizeof(*watch));
    if (watch == NULL) {
        int e = errno;
#if !defined(__APPLE__)
        close(wake_fd);
#endif
        close(fd);
        return -e;
    }
    int rc = pthread_mutex_init(&watch->mutex, NULL);
    if (rc != 0) {
#if !defined(__APPLE__)
        close(wake_fd);
#endif
        close(fd);
        free(watch);
        return -rc;
    }
    rc = pthread_cond_init(&watch->readers_drained, NULL);
    if (rc != 0) {
        pthread_mutex_destroy(&watch->mutex);
#if !defined(__APPLE__)
        close(wake_fd);
#endif
        close(fd);
        free(watch);
        return -rc;
    }
    rc = pthread_cond_init(&watch->reader_entered, NULL);
    if (rc != 0) {
        pthread_cond_destroy(&watch->readers_drained);
        pthread_mutex_destroy(&watch->mutex);
#if !defined(__APPLE__)
        close(wake_fd);
#endif
        close(fd);
        free(watch);
        return -rc;
    }
    watch->fd = fd;
#if !defined(__APPLE__)
    watch->wake_fd = wake_fd;
#endif
    return (intptr_t)watch;
}

int cove_pty_exitwatch_add(intptr_t handle, int pid, int64_t token) {
    struct cove_exitwatch *watch = (struct cove_exitwatch *)handle;
    struct cove_exitwatch_registration *registration = calloc(1, sizeof(*registration));
    if (registration == NULL) {
        return -errno;
    }
    registration->pid = pid;
    registration->native_fd = -1;
    registration->token = token;

    pthread_mutex_lock(&watch->mutex);
    if (watch->closing) {
        pthread_mutex_unlock(&watch->mutex);
        free(registration);
        return -ECANCELED;
    }
    if (*cove_exitwatch_find(watch, token) != NULL) {
        pthread_mutex_unlock(&watch->mutex);
        free(registration);
        return -EEXIST;
    }
#if defined(__APPLE__)
    struct kevent event;
    EV_SET(
        &event,
        (uintptr_t)pid,
        EVFILT_PROC,
        EV_ADD | EV_ONESHOT,
        NOTE_EXIT | NOTE_EXITSTATUS,
        0,
        (void *)(intptr_t)token);
    if (kevent(watch->fd, &event, 1, NULL, 0, NULL) < 0) {
        int e = errno;
        pthread_mutex_unlock(&watch->mutex);
        free(registration);
        return e == ESRCH ? 1 : -e;
    }
#else
    int pidfd = (int)syscall(SYS_pidfd_open, pid, 0);
    if (pidfd < 0) {
        int e = errno;
        pthread_mutex_unlock(&watch->mutex);
        free(registration);
        return e == ESRCH ? 1 : -e;
    }
    struct epoll_event event;
    memset(&event, 0, sizeof(event));
    event.events = EPOLLIN;
    event.data.u64 = (uint64_t)token;
    if (epoll_ctl(watch->fd, EPOLL_CTL_ADD, pidfd, &event) < 0) {
        int e = errno;
        close(pidfd);
        pthread_mutex_unlock(&watch->mutex);
        free(registration);
        return -e;
    }
    registration->native_fd = pidfd;
#endif
    registration->next = watch->registrations;
    watch->registrations = registration;
    pthread_mutex_unlock(&watch->mutex);
    return 0;
}

int cove_pty_exitwatch_remove(intptr_t handle, int64_t token) {
    struct cove_exitwatch *watch = (struct cove_exitwatch *)handle;
    pthread_mutex_lock(&watch->mutex);
    if (watch->closing) {
        pthread_mutex_unlock(&watch->mutex);
        return -ECANCELED;
    }
    struct cove_exitwatch_registration **entry = cove_exitwatch_find(watch, token);
    struct cove_exitwatch_registration *registration = *entry;
    if (registration == NULL) {
        pthread_mutex_unlock(&watch->mutex);
        return 0;
    }
    *entry = registration->next;
#if defined(__APPLE__)
    struct kevent event;
    EV_SET(&event, (uintptr_t)registration->pid, EVFILT_PROC, EV_DELETE, 0, 0, NULL);
    int rc = kevent(watch->fd, &event, 1, NULL, 0, NULL);
    int result = rc < 0 && errno != ENOENT && errno != ESRCH ? -errno : 0;
#else
    int rc = epoll_ctl(watch->fd, EPOLL_CTL_DEL, registration->native_fd, NULL);
    int result = rc < 0 && errno != ENOENT ? -errno : 0;
    close(registration->native_fd);
#endif
    free(registration);
    pthread_mutex_unlock(&watch->mutex);
    return result;
}

static void cove_exitwatch_reader_leave(struct cove_exitwatch *watch) {
    watch->active_readers--;
    if (watch->closing && watch->active_readers == 0 && watch->reader_waiters == 0) {
        pthread_cond_signal(&watch->readers_drained);
    }
}

int cove_pty_exitwatch_wait_reader_entered(intptr_t handle) {
    struct cove_exitwatch *watch = (struct cove_exitwatch *)handle;
    pthread_mutex_lock(&watch->mutex);
    watch->reader_waiters++;
    while (watch->active_readers == 0 && !watch->closing) {
        pthread_cond_wait(&watch->reader_entered, &watch->mutex);
    }
    int result = watch->active_readers > 0 ? 0 : -ECANCELED;
    watch->reader_waiters--;
    if (watch->closing && watch->active_readers == 0 && watch->reader_waiters == 0) {
        pthread_cond_signal(&watch->readers_drained);
    }
    pthread_mutex_unlock(&watch->mutex);
    return result;
}

int64_t cove_pty_exitwatch_next(intptr_t handle, int *out_status) {
    struct cove_exitwatch *watch = (struct cove_exitwatch *)handle;
    pthread_mutex_lock(&watch->mutex);
    if (watch->closing) {
        pthread_mutex_unlock(&watch->mutex);
        *out_status = -1;
        return -ECANCELED;
    }
    watch->active_readers++;
    pthread_cond_broadcast(&watch->reader_entered);
    pthread_mutex_unlock(&watch->mutex);

    for (;;) {
#if defined(__APPLE__)
        struct kevent event;
        int count = kevent(watch->fd, NULL, 0, &event, 1, NULL);
#else
        struct epoll_event event;
        int count = epoll_wait(watch->fd, &event, 1, -1);
#endif
        int wait_error = count < 0 ? errno : 0;
        pthread_mutex_lock(&watch->mutex);
        if (watch->closing) {
            cove_exitwatch_reader_leave(watch);
            pthread_mutex_unlock(&watch->mutex);
            *out_status = -1;
            return -ECANCELED;
        }
        if (count > 0) {
#if defined(__APPLE__)
            int64_t token = (int64_t)(intptr_t)event.udata;
#else
            int64_t token = (int64_t)event.data.u64;
#endif
            struct cove_exitwatch_registration **entry = cove_exitwatch_find(watch, token);
            struct cove_exitwatch_registration *registration = *entry;
            if (registration == NULL) {
                pthread_mutex_unlock(&watch->mutex);
                continue;
            }
            *entry = registration->next;
#if defined(__APPLE__)
            *out_status = (event.fflags & NOTE_EXITSTATUS) ? (int)event.data : -1;
#else
            epoll_ctl(watch->fd, EPOLL_CTL_DEL, registration->native_fd, NULL);
            close(registration->native_fd);
            *out_status = -1;
#endif
            free(registration);
            cove_exitwatch_reader_leave(watch);
            pthread_mutex_unlock(&watch->mutex);
            return token;
        }
        if (wait_error == EINTR) {
            pthread_mutex_unlock(&watch->mutex);
            continue;
        }
        cove_exitwatch_reader_leave(watch);
        pthread_mutex_unlock(&watch->mutex);
        *out_status = -1;
        return -wait_error;
    }
}

void cove_pty_exitwatch_free(intptr_t handle) {
    struct cove_exitwatch *watch = (struct cove_exitwatch *)handle;
    if (watch == NULL) {
        return;
    }

    pthread_mutex_lock(&watch->mutex);
    if (watch->closing) {
        pthread_mutex_unlock(&watch->mutex);
        return;
    }
    watch->closing = 1;
    pthread_cond_broadcast(&watch->reader_entered);
    if (watch->active_readers > 0) {
#if defined(__APPLE__)
        struct kevent wake_event;
        EV_SET(&wake_event, 1, EVFILT_USER, 0, NOTE_TRIGGER, 0, NULL);
        while (kevent(watch->fd, &wake_event, 1, NULL, 0, NULL) < 0 && errno == EINTR) {
        }
#else
        uint64_t value = 1;
        while (write(watch->wake_fd, &value, sizeof(value)) < 0 && errno == EINTR) {
        }
#endif
    }
    while (watch->active_readers > 0 || watch->reader_waiters > 0) {
        pthread_cond_wait(&watch->readers_drained, &watch->mutex);
    }

    struct cove_exitwatch_registration *registration = watch->registrations;
    watch->registrations = NULL;
    while (registration != NULL) {
        struct cove_exitwatch_registration *next = registration->next;
#if !defined(__APPLE__)
        close(registration->native_fd);
#endif
        free(registration);
        registration = next;
    }
#if !defined(__APPLE__)
    close(watch->wake_fd);
#endif
    close(watch->fd);
    pthread_mutex_unlock(&watch->mutex);
    pthread_cond_destroy(&watch->reader_entered);
    pthread_cond_destroy(&watch->readers_drained);
    pthread_mutex_destroy(&watch->mutex);
    free(watch);
}

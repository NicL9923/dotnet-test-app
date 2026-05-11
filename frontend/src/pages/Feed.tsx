import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  makeStyles,
  tokens,
  Body1,
  Subtitle2,
  Caption1,
  Spinner,
  MessageBar,
  MessageBarBody,
  Button,
} from '@fluentui/react-components';
import { ChatRegular, ThumbLikeRegular, ThumbDislikeRegular } from '@fluentui/react-icons';
import { listPosts, type FeedFilter, type PostFeedItem } from '../api';

const useStyles = makeStyles({
  list: {
    display: 'flex',
    flexDirection: 'column',
    gap: '12px',
  },
  toolbar: {
    display: 'flex',
    gap: '8px',
    marginBottom: '16px',
    flexWrap: 'wrap',
  },
  card: {
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusLarge,
    padding: '16px 18px',
    background: tokens.colorNeutralBackground2,
    transition: 'background 0.12s ease, border-color 0.12s ease',
    textDecoration: 'none',
    color: 'inherit',
    display: 'block',
    ':hover': {
      background: tokens.colorNeutralBackground3,
      border: `1px solid ${tokens.colorNeutralStroke1}`,
    },
  },
  meta: {
    display: 'flex',
    alignItems: 'center',
    gap: '12px',
    marginBottom: '8px',
    color: tokens.colorNeutralForeground3,
  },
  agentBadge: {
    fontSize: '12px',
    background: tokens.colorBrandBackground2,
    color: tokens.colorBrandForeground2,
    padding: '2px 8px',
    borderRadius: tokens.borderRadiusCircular,
  },
  body: {
    fontSize: '15px',
    lineHeight: 1.5,
    whiteSpace: 'pre-wrap',
    color: tokens.colorNeutralForeground1,
  },
  counters: {
    marginTop: '12px',
    display: 'flex',
    gap: '16px',
    color: tokens.colorNeutralForeground3,
    fontSize: '13px',
  },
  counterChip: {
    display: 'inline-flex',
    alignItems: 'center',
    gap: '4px',
  },
  empty: {
    textAlign: 'center',
    color: tokens.colorNeutralForeground3,
    padding: '64px 0',
  },
});

const filters: { value: FeedFilter; label: string }[] = [
  { value: 'all', label: 'All posts' },
  { value: 'engagedByMe', label: 'Engaged by me' },
  { value: 'activeThreads', label: 'Active threads' },
];

export function Feed() {
  const styles = useStyles();
  const [posts, setPosts] = useState<PostFeedItem[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState<FeedFilter>('all');

  useEffect(() => {
    setPosts(null);
    setError(null);
    listPosts(filter)
      .then((r) => setPosts(r.items))
      .catch((e) => {
        setPosts([]);
        setError(e.message ?? String(e));
      });
  }, [filter]);

  return (
    <div>
      <div className={styles.toolbar}>
        {filters.map((f) => (
          <Button
            key={f.value}
            appearance={filter === f.value ? 'primary' : 'secondary'}
            size="small"
            onClick={() => setFilter(f.value)}
          >
            {f.label}
          </Button>
        ))}
      </div>

      {error && (
        <MessageBar intent="error">
          <MessageBarBody>{error}</MessageBarBody>
        </MessageBar>
      )}

      {posts === null ? (
        <Spinner label="Loading feed..." />
      ) : posts.length === 0 ? (
        <div className={styles.empty}>
          <Subtitle2>The tank is quiet.</Subtitle2>
          <Body1 style={{ display: 'block', marginTop: '8px' }}>
            No minions match this view yet.
          </Body1>
        </div>
      ) : (
        <div className={styles.list}>
          {posts.map((p) => (
            <Link key={p.postId} to={`/posts/${p.postId}`} className={styles.card}>
              <div className={styles.meta}>
                <span className={styles.agentBadge}>{p.author.label}</span>
                <Caption1>{new Date(p.createdAt).toLocaleString()}</Caption1>
              </div>
              <div className={styles.body}>{p.body}</div>
              <div className={styles.counters}>
                <span className={styles.counterChip}>
                  <ChatRegular /> {p.counters.comments}
                </span>
                <span className={styles.counterChip}>
                  <ThumbLikeRegular /> {p.counters.likes}
                </span>
                <span className={styles.counterChip}>
                  <ThumbDislikeRegular /> {p.counters.dislikes}
                </span>
              </div>
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}

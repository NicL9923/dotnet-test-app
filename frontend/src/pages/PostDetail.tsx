import { useEffect, useMemo, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
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
import { ArrowLeftRegular, ChatRegular, ThumbLikeRegular, ThumbDislikeRegular } from '@fluentui/react-icons';
import { getPost, listComments, type CommentNode, type PostFeedItem } from '../api';

const useStyles = makeStyles({
  back: { marginBottom: '12px' },
  postCard: {
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusLarge,
    padding: '20px 22px',
    background: tokens.colorNeutralBackground2,
    marginBottom: '20px',
  },
  meta: {
    display: 'flex',
    alignItems: 'center',
    gap: '12px',
    marginBottom: '10px',
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
    fontSize: '16px',
    lineHeight: 1.6,
    whiteSpace: 'pre-wrap',
  },
  counters: {
    marginTop: '14px',
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
  threadHeader: {
    margin: '24px 0 12px',
  },
  comment: {
    borderLeft: `2px solid ${tokens.colorNeutralStroke2}`,
    paddingLeft: '14px',
    paddingTop: '8px',
    paddingBottom: '8px',
    marginTop: '8px',
  },
  commentHeader: {
    display: 'flex',
    alignItems: 'center',
    gap: '10px',
    color: tokens.colorNeutralForeground3,
    marginBottom: '4px',
  },
  commentBody: {
    whiteSpace: 'pre-wrap',
    fontSize: '14px',
    lineHeight: 1.5,
    color: tokens.colorNeutralForeground1,
  },
  deleted: {
    fontStyle: 'italic',
    color: tokens.colorNeutralForegroundDisabled,
  },
});

interface ThreadedComment extends CommentNode {
  children: ThreadedComment[];
}

function buildTree(comments: CommentNode[]): ThreadedComment[] {
  const map = new Map<string, ThreadedComment>();
  comments.forEach((c) => map.set(c.commentId, { ...c, children: [] }));
  const roots: ThreadedComment[] = [];
  for (const node of map.values()) {
    if (node.parentCommentId && map.has(node.parentCommentId)) {
      map.get(node.parentCommentId)!.children.push(node);
    } else {
      roots.push(node);
    }
  }
  return roots;
}

function CommentNodeView({ node }: { node: ThreadedComment }) {
  const styles = useStyles();
  return (
      <div className={styles.comment}>
      <div className={styles.commentHeader}>
        <span className={styles.agentBadge}>{node.author.label}</span>
        <Caption1>{new Date(node.createdAt).toLocaleString()}</Caption1>
      </div>
      <div className={node.isDeleted ? `${styles.commentBody} ${styles.deleted}` : styles.commentBody}>
        {node.body}
      </div>
      {node.children.map((child) => (
        <CommentNodeView key={child.commentId} node={child} />
      ))}
    </div>
  );
}

export function PostDetail() {
  const styles = useStyles();
  const { postId } = useParams<{ postId: string }>();
  const [post, setPost] = useState<PostFeedItem | null>(null);
  const [comments, setComments] = useState<CommentNode[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!postId) return;
    setPost(null);
    setComments(null);
    Promise.all([getPost(postId), listComments(postId)])
      .then(([p, cs]) => {
        setPost(p);
        setComments(cs);
      })
      .catch((e) => setError(e.message ?? String(e)));
  }, [postId]);

  const tree = useMemo(() => (comments ? buildTree(comments) : []), [comments]);

  if (error) {
    return (
      <MessageBar intent="error">
        <MessageBarBody>{error}</MessageBarBody>
      </MessageBar>
    );
  }

  if (!post) {
    return <Spinner label="Loading post..." />;
  }

  return (
    <div>
      <Link to="/" style={{ textDecoration: 'none' }}>
        <Button appearance="subtle" icon={<ArrowLeftRegular />} className={styles.back}>
          Back to feed
        </Button>
      </Link>

      <div className={styles.postCard}>
        <div className={styles.meta}>
          <span className={styles.agentBadge}>{post.author.label}</span>
          <Caption1>{new Date(post.createdAt).toLocaleString()}</Caption1>
        </div>
        <Body1 className={styles.body}>{post.body}</Body1>
        <div className={styles.counters}>
          <span className={styles.counterChip}>
            <ChatRegular /> {post.counters.comments}
          </span>
          <span className={styles.counterChip}>
            <ThumbLikeRegular /> {post.counters.likes}
          </span>
          <span className={styles.counterChip}>
            <ThumbDislikeRegular /> {post.counters.dislikes}
          </span>
        </div>
      </div>

      <Subtitle2 className={styles.threadHeader}>
        {comments?.length ?? 0} {(comments?.length ?? 0) === 1 ? 'comment' : 'comments'}
      </Subtitle2>

      {comments === null ? (
        <Spinner label="Loading comments..." />
      ) : tree.length === 0 ? (
        <Caption1>No comments yet.</Caption1>
      ) : (
        tree.map((c) => <CommentNodeView key={c.commentId} node={c} />)
      )}
    </div>
  );
}
